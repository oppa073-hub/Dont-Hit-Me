using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 3D 팔: 코드 IK로 핸들을 잡고 손가락을 굽힌다
public partial class DriveGame
{
    class Finger
    {
        public Transform[] bones;      // 1,2,3 마디
        public Transform tip;          // _end
        public Quaternion[] rest;
        public bool thumb;             // 최대 굽힘각은 fingerCurl / thumbCurl
        public Vector3 axisLocal;      // 손목 로컬 굽힘축 (+각도로 돌리면 손바닥 쪽으로 굽는다)
    }

    class ArmIK
    {
        public Transform upper, fore, wrist, twist0, twist1;
        public float lenU, lenF;
        public Transform target;
        public float baseAngle;        // 핸들이 안 돌았을 때 잡는 위치 (도)
        public float gripAngle;        // 지금 잡는 위치 (라디안, 핸들 로컬)
        public float thumbSense = 1f;  // 엄지가 향할 림 방향(반시계 +1 / 시계 -1). 핸들을 돌려도 손이 갑자기 뒤집히지 않게 고정
        public Quaternion restUpper, restFore, restWrist, restTwist0, restTwist1;   // 에셋 원래 자세 (로컬 회전)
        // 에셋 기본 자세에서 잰 손 모양: 손가락 방향 F, 손바닥 방향 P (손목 로컬), 좌우(chir), 손목→쥐는 점(socket)
        public Vector3 fwdLocal, palmLocal, socketK;
        public float chir = 1f;        // cross(F, P) 가 엄지 쪽이면 +1, 반대면 -1 (왼손/오른손)
        public bool handOk;
        public Vector3 gripPoint, wristGoal, fwdD, palmD;   // 이번 프레임 목표
        public Vector3 poleLocal;
        public List<Finger> fingers = new List<Finger>();
    }

    ArmIK armLeft, armRight;

    bool arms3D;

    bool SetupArms()
    {
        GameObject go;
        try
        {
            go = Instantiate(armsModel, viewRoot);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("DriveGame: arms.fbx 를 생성하지 못해 2D 팔로 대체합니다. (" + e.Message + ")  씬의 Arms Model 을 arms.fbx 로 다시 지정하세요.");
            return false;
        }
        go.name = "Arms";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var anim = go.GetComponentInChildren<Animator>();
        if (anim != null) anim.enabled = false;

        var bones = new Dictionary<string, Transform>();
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        string[] need = { "bicep.l", "forearm.l", "wrist.l", "bicep.r", "forearm.r", "wrist.r" };
        foreach (var n in need)
        {
            if (!bones.ContainsKey(n))
            {
                Debug.LogWarning("DriveGame: arms.fbx 에서 본 '" + n + "' 을 찾지 못해 2D 팔로 대체합니다.");
                Destroy(go);
                return false;
            }
        }

        // 재질: URP 에서 분홍색으로 깨지는 것을 막기 위해 직접 만든 Lit 재질로 교체
        var mat = new Material(litShader);
        mat.SetColor("_BaseColor", Color.white);
        armMaterial = mat;                                   // 분노 게이지에 따라 붉게 칠한다 (DriveGame.Rage.cs)
        if (armAlbedo != null)
        {
            mat.SetTexture("_BaseMap", armAlbedo);
            mat.mainTexture = armAlbedo;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null) smr.updateWhenOffscreen = true;
        }

        // 1) 팔 길이에 맞춰 스케일 (모델 단위를 모르므로 본 거리로 측정)
        float rawLen = Vector3.Distance(bones["bicep.l"].position, bones["forearm.l"].position)
                     + Vector3.Distance(bones["forearm.l"].position, bones["wrist.l"].position);
        if (rawLen < 1e-5f)
        {
            Destroy(go);
            return false;
        }
        go.transform.localScale = Vector3.one * (armLength / rawLen);

        // 2) 두 어깨를 카메라 X축에 맞추도록 Y회전, 어깨 중점을 shoulderCenter 로 이동
        Vector3 bl = viewRoot.InverseTransformPoint(bones["bicep.l"].position);
        Vector3 br = viewRoot.InverseTransformPoint(bones["bicep.r"].position);
        Vector3 axis = br - bl;
        axis.y = 0f;
        if (axis.sqrMagnitude > 1e-8f)
            go.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.right);
        bl = viewRoot.InverseTransformPoint(bones["bicep.l"].position);
        br = viewRoot.InverseTransformPoint(bones["bicep.r"].position);
        go.transform.localPosition += shoulderCenter - (bl + br) * 0.5f;

        // 3) 화면 왼쪽에 있는 쪽을 왼팔로 취급
        bl = viewRoot.InverseTransformPoint(bones["bicep.l"].position);
        br = viewRoot.InverseTransformPoint(bones["bicep.r"].position);
        string leftSide = bl.x <= br.x ? "l" : "r";
        string rightSide = leftSide == "l" ? "r" : "l";

        armLeft = MakeArm(bones, leftSide, 180f - handAngle, new Vector3(-0.5f, -1f, -0.3f));
        armRight = MakeArm(bones, rightSide, handAngle, new Vector3(0.5f, -1f, -0.3f));
        return true;
    }

    ArmIK MakeArm(Dictionary<string, Transform> bones, string side, float angleDeg, Vector3 pole)
    {
        var a = new ArmIK
        {
            upper = bones["bicep." + side],
            fore = bones["forearm." + side],
            wrist = bones["wrist." + side],
            poleLocal = pole,
            baseAngle = angleDeg,
            gripAngle = angleDeg * Mathf.Deg2Rad,
            // 기준 위치에서 반시계 접선이 핸들 위쪽을 향하면 +1 (오른손 2시 = +1, 왼손 10시 = -1)
            thumbSense = Mathf.Cos(angleDeg * Mathf.Deg2Rad) >= 0f ? 1f : -1f
        };
        bones.TryGetValue("forearm.Twist0." + side, out a.twist0);
        bones.TryGetValue("forearm.Twist1." + side, out a.twist1);
        a.restUpper = a.upper.localRotation;
        a.restFore = a.fore.localRotation;
        a.restWrist = a.wrist.localRotation;
        if (a.twist0 != null) a.restTwist0 = a.twist0.localRotation;
        if (a.twist1 != null) a.restTwist1 = a.twist1.localRotation;
        a.lenU = Vector3.Distance(a.upper.position, a.fore.position);
        a.lenF = Vector3.Distance(a.fore.position, a.wrist.position);

        var tgt = new GameObject("GripTarget." + side).transform;
        tgt.SetParent(wheelSpin, false);
        a.target = tgt;

        // 손 모양 측정 (에셋 기본 자세). 손가락은 기본 자세에서 살짝 굽어 있으므로, 가운뎃손가락 끝이 처진 쪽이 손바닥이다.
        // socket 본 = 무기 손잡이가 놓이는 자리(주먹 안). 여기에 림을 넣으면 주먹이 림을 감싼다.
        bones.TryGetValue("finger_middle1." + side, out var mid1);
        bones.TryGetValue("finger_index1." + side, out var idx1);
        bones.TryGetValue("finger_pinky1." + side, out var pinky1);
        bones.TryGetValue("finger_middle3." + side + "_end", out var midTip);
        bones.TryGetValue("socket." + side, out var socket);
        if (mid1 != null && idx1 != null && pinky1 != null && midTip != null && mid1.IsChildOf(a.wrist))
        {
            Vector3 W = a.wrist.position;
            Vector3 F = (mid1.position - W).normalized;
            Vector3 P = Vector3.ProjectOnPlane(midTip.position - W, F);
            if (P.sqrMagnitude > 1e-10f)
            {
                P.Normalize();
                Vector3 L = Vector3.Cross(F, P);
                a.chir = Vector3.Dot(L, idx1.position - pinky1.position) >= 0f ? 1f : -1f;
                Vector3 off = socket != null && socket.IsChildOf(a.wrist)
                    ? socket.position - W
                    : (mid1.position - W) * 0.9f + P * Vector3.Distance(mid1.position, W) * 0.4f;
                a.socketK = new Vector3(Vector3.Dot(off, F), Vector3.Dot(off, P), Vector3.Dot(off, L));
                a.fwdLocal = a.wrist.InverseTransformDirection(F);
                a.palmLocal = a.wrist.InverseTransformDirection(P);
                a.handOk = true;
            }
        }
        if (!a.handOk)
            Debug.LogWarning("DriveGame: 손가락 본으로 손 모양을 재지 못해 손 방향/손가락 쥐기를 건너뜁니다. (" + side + ")");

        // 손가락: 마디 3개 + 끝 본. 굽힘축 = 첫 마디 방향 x 손바닥 방향 (+각도면 손바닥 쪽으로 굽는다)
        string[] names = { "pinky", "ring", "middle", "index", "thumb" };
        foreach (var n in names)
        {
            var f = new Finger { bones = new Transform[3], rest = new Quaternion[3] };
            bool ok = true;
            for (int k = 0; k < 3 && ok; k++)
            {
                string key = "finger_" + n + (k + 1) + "." + side;
                if (bones.TryGetValue(key, out var b)) { f.bones[k] = b; f.rest[k] = b.localRotation; }
                else ok = false;
            }
            string endKey = "finger_" + n + "3." + side + "_end";
            if (!ok || !bones.TryGetValue(endKey, out f.tip)) continue;
            f.thumb = n == "thumb";
            if (a.handOk)
            {
                Vector3 P = a.wrist.TransformDirection(a.palmLocal);
                Vector3 ax = Vector3.Cross(f.bones[1].position - f.bones[0].position, P);
                if (ax.sqrMagnitude < 1e-10f) ax = Vector3.Cross(a.wrist.TransformDirection(a.fwdLocal), P);
                f.axisLocal = a.wrist.InverseTransformDirection(ax.normalized);
            }
            a.fingers.Add(f);
        }
        return a;
    }

    // 손이 잡는 위치와 손 방향 목표.
    // - 핸들이 steerAngle 만큼 돌아도 손은 gripFollow 비율만 따라가고, 나머지는 림 위를 미끄러진다 (핸들은 -steerAngle 만큼 돈다)
    // - 막대를 쥔 주먹: 네 손가락이 림 접선을 따라 쌓이고, 손가락 방향은 림 단면(바깥 R ~ 앞유리 N) 안에서 gripRoll 각도
    // - 손목 위치 = 림 중심선 위의 점 - (손목→socket). 그래서 socket(주먹 안)에 림이 들어간다
    void UpdateGrip(ArmIK a)
    {
        float phi = a.baseAngle + (1f - gripFollow) * steerAngle;
        float r = phi * Mathf.Deg2Rad;
        a.gripAngle = r;
        Vector3 radialL = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0f);
        a.target.localPosition = radialL * wheelRadius;
        a.gripPoint = a.target.position;
        if (!a.handOk)
        {
            a.wristGoal = a.gripPoint;
            return;
        }

        Vector3 R = wheelSpin.TransformDirection(radialL);
        Vector3 N = wheelSpin.forward;
        Vector3 thumbDir = wheelSpin.TransformDirection(new Vector3(-Mathf.Sin(r), Mathf.Cos(r), 0f)) * a.thumbSense * (thumbUp ? 1f : -1f);
        float th = gripRoll * Mathf.Deg2Rad;
        a.fwdD = (R * Mathf.Cos(th) + N * Mathf.Sin(th)).normalized;
        a.palmD = a.chir * Vector3.Cross(thumbDir, a.fwdD);
        Vector3 L = Vector3.Cross(a.fwdD, a.palmD);
        Vector3 off = a.fwdD * a.socketK.x + a.palmD * a.socketK.y + L * a.socketK.z;
        Vector3 nudge = a.fwdD * gripNudge.x + a.palmD * gripNudge.y + thumbDir * gripNudge.z;
        a.wristGoal = a.gripPoint - off + nudge;
    }

    // 2본 IK: 어깨→팔꿈치→손목. 매 프레임 에셋 원래 자세에서 시작하므로 회전이 누적되어 틀어지지 않는다.
    // 본 방향 규약을 몰라도 되도록 실제 자식 본 위치를 기준으로 회전시킨다.
    void SolveArm(ArmIK a)
    {
        a.upper.localRotation = a.restUpper;
        a.fore.localRotation = a.restFore;
        a.wrist.localRotation = a.restWrist;
        if (a.twist0 != null) a.twist0.localRotation = a.restTwist0;
        if (a.twist1 != null) a.twist1.localRotation = a.restTwist1;

        Vector3 S = a.upper.position;
        Vector3 T = a.wristGoal;
        float d0 = (T - S).magnitude;
        if (d0 < 1e-5f) return;
        float d = Mathf.Clamp(d0, Mathf.Abs(a.lenU - a.lenF) + 0.01f, a.lenU + a.lenF - 0.005f);
        Vector3 dir = (T - S) / d0;
        T = S + dir * d;

        float along = (a.lenU * a.lenU - a.lenF * a.lenF + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, a.lenU * a.lenU - along * along));
        Vector3 pole = viewRoot.TransformDirection(a.poleLocal.normalized);
        Vector3 perp = pole - Vector3.Dot(pole, dir) * dir;
        perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.down;
        Vector3 elbow = S + dir * along + perp * h;

        a.upper.rotation = Quaternion.FromToRotation(a.fore.position - S, elbow - S) * a.upper.rotation;
        a.fore.rotation = Quaternion.FromToRotation(a.wrist.position - a.fore.position, T - a.fore.position) * a.fore.rotation;
    }

    // 손 방향: 측정해 둔 (손가락 F, 손바닥 P) 를 목표 (fwdD, palmD) 에 그대로 맞춘다. 왼손/오른손은 chir 로 이미 반영됨.
    // 손목이 팔뚝 축으로 돈 만큼을 팔뚝 비틀림 본(Twist0/1)에 1/3 씩 나눠 줘서 팔뚝 피부가 꼬이지 않게 한다.
    void OrientHand(ArmIK a)
    {
        if (!a.handOk) return;
        Vector3 fH = a.wrist.TransformDirection(a.fwdLocal);
        Vector3 pH = a.wrist.TransformDirection(a.palmLocal);
        a.wrist.rotation = Quaternion.LookRotation(a.fwdD, a.palmD) * Quaternion.Inverse(Quaternion.LookRotation(fH, pH)) * a.wrist.rotation;

        if (a.twist0 == null || a.wrist.parent == null) return;
        Vector3 axis = (a.wrist.position - a.fore.position).normalized;
        Vector3 pRest = a.wrist.parent.rotation * (a.restWrist * a.palmLocal);
        Vector3 pNow = a.wrist.TransformDirection(a.palmLocal);
        float tw = Vector3.SignedAngle(Vector3.ProjectOnPlane(pRest, axis), Vector3.ProjectOnPlane(pNow, axis), axis);
        a.twist0.rotation = Quaternion.AngleAxis(tw / 3f, axis) * a.twist0.rotation;
        if (a.twist1 != null) a.twist1.rotation = Quaternion.AngleAxis(tw / 3f, axis) * a.twist1.rotation;
    }

    // 손가락 쥐기: 손바닥 쪽으로 조금씩 굽히다가 손가락 끝이 림 표면에 닿는 순간 멈춘다.
    // 핸들이 돌거나 손 위치가 바뀌어도 매 프레임 다시 찾으므로 림을 파고들거나 허공을 쥐지 않는다.
    void CurlFingers(ArmIK a)
    {
        float surface = RimTube + fingerPad;
        foreach (var f in a.fingers)
        {
            if (!a.handOk)
            {
                for (int k = 0; k < 3; k++) f.bones[k].localRotation = f.rest[k];
                continue;
            }
            Vector3 axis = a.wrist.TransformDirection(f.axisLocal);
            Vector3 maxAng = f.thumb ? thumbCurl : fingerCurl;
            // 최소 minCurl 만큼은 항상 굽히고, 림에 닿지 않는 손가락(림 아래로 삐져나온 새끼/약지 등)은 끝까지 오므린다
            const int steps = 20;
            float best = 1f;
            bool wasOut = false;
            for (int i = 0; i <= steps; i++)
            {
                float s = Mathf.Lerp(minCurl, 1f, i / (float)steps);
                PoseFinger(f, axis, maxAng * s);
                float dist = RimDist(f.tip.position);
                if (dist > surface) wasOut = true;
                else if (wasOut) { best = s; break; }
            }
            PoseFinger(f, axis, maxAng * best);
        }
    }

    static void PoseFinger(Finger f, Vector3 axis, Vector3 ang)
    {
        for (int k = 0; k < 3; k++) f.bones[k].localRotation = f.rest[k];
        for (int k = 0; k < 3; k++)
        {
            float g = k == 0 ? ang.x : (k == 1 ? ang.y : ang.z);
            f.bones[k].rotation = Quaternion.AngleAxis(g, axis) * f.bones[k].rotation;
        }
    }

    // 점에서 림(원형 튜브의 중심선)까지 거리
    float RimDist(Vector3 p)
    {
        Vector3 l = wheelSpin.InverseTransformPoint(p);
        float rho = new Vector2(l.x, l.y).magnitude;
        return new Vector2(rho - wheelRadius, l.z).magnitude;
    }
}
