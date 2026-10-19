using UnityEngine;

// 손님 유형 한 종류의 데이터. 값은 PassengerCatalog.PaxTypes 표에서 고친다.
// 손님 유형 표: 여기만 고치면 이름/대사/목적지/요금이 바뀐다. hello 의 {0} 은 목적지 이름.
// 조사 때문에 {0} 뒤에는 "까지"처럼 받침과 상관없는 말만 쓴다 (예: "{0}까지 가 주세요").
// 손님 유별 사건 종류 (PaxType.quirk)
public static class Quirk
{
    public const int None = 0, Bomb = 1, Wedding = 2, Alien = 3, Racer = 4, Fighter = 5;
}

public class PaxType
{
    public string[] names, dests, hello, react, thanks;
    public float fareMul = 1f;       // 이 유형의 기본 요금 배율
    public float hitSense = 1f;      // 사람을 쳤을 때 감액이 얼마나 커지는지 (1 = hitFareMul 그대로, 클수록 더 깎음)
    public float timeMul = 1f;       // 허용 시간 배율 (작을수록 급한 손님)
    public float tipMul = 1f;        // 팁 배율
    public int hair = -1;            // HairTones 번호. -1 이면 무작위
    public int weight = 2;           // 나타날 확률 가중치
    public string[] chat;            // 타고 가는 동안 가끔 걸어 오는 말 (없으면 조용한 손님)
    public float swerveSense = 0f;   // 급하게 핸들을 꺾을 때 멀미하는 정도 (0 = 상관없음, 클수록 쉽게 멀미)
    public bool heist;               // 은행 강도 손님 (랜덤 인카운트로만 나온다. weight 는 0)
    public bool bigTalk;             // 말이 화면 가운데에 크게 나온다 (수다쟁이만: 말로 화면을 가리는 것이 그 손님의 특징)
    public int quirk;                // 손님 유별 사건 (Quirk 상수: 폭탄, 결혼식, 외계인, 레이서, 폭주)
}

// 손님 데이터 표: 여기만 고치면 이름/대사/목적지/요금이 바뀐다. (대사를 늘리려면 이 파일만 열면 된다)
public static class PassengerCatalog
{
    public static readonly PaxType[] PaxTypes =
    {
        new PaxType   // 회사원: 요금 높고 사고에 엄격하다
        {
            names = new[] { "해리스 씨‖Mr. Harris", "코헨 씨‖Mr. Cohen", "로시 씨‖Ms. Rossi" },
            dests = new[] { "시청‖City Hall", "공항‖JFK Airport", "호텔‖Midtown Hotel", "중앙역‖Grand Central" },
            hello = new[] { "{0}까지 가 주세요. 미팅에 늦었어요.‖{0}, please. I'm late for a meeting.", "기사님, {0}까지. 급합니다.‖Driver, {0}. It's urgent.", "{0}까지요. 최대한 빨리, 하지만 안전하게.‖{0}. As fast as you can — but safely." },
            react = new[] { "이봐요, 운전 똑바로 합시다!‖Hey, drive properly, buddy!", "지금 뭐 하시는 겁니까!‖What do you think you're doing!", "사람을 칠 뻔했잖아요!‖You almost hit someone!" },
            thanks = new[] { "수고했어요. 잔돈은 됐습니다.‖Good work. Keep the change.", "시간 맞춰 왔군요. 좋아요.‖Right on time. Good." },
            fareMul = 1.2f, hitSense = 1.3f, timeMul = 0.85f, tipMul = 1.5f
        },
        new PaxType   // 할머니: 느긋하고 놀람이 크다
        {
            names = new[] { "베티 할머니‖Grandma Betty", "로즈 할머니‖Grandma Rose", "준 할머니‖Grandma June" },
            dests = new[] { "대학병원‖University Hospital", "시장‖Farmers Market", "공원 입구‖Central Park Gate" },
            hello = new[] { "아이고, 택시 잡았네. {0}까지 천천히 가 주오.‖Oh my, I caught a cab. {0}, nice and slow, dear.", "기사 양반, {0}까지 가 주시게.‖Driver, take me to {0}, would you?", "{0}에 가요. 서두르지 말아요.‖Going to {0}. Don't rush." },
            react = new[] { "아이고 깜짝이야!‖Oh my goodness!", "아이구, 이게 무슨 일이래!‖Good heavens, what was that!", "아이고 놀래라!!‖Oh, you scared me!!" },
            thanks = new[] { "고마워요, 젊은이. 복 받을 거야.‖Thank you, young man. Bless you.", "안전하게 데려다줘서 고맙소.‖Thanks for getting me here safely." },
            fareMul = 1.0f, hitSense = 1.0f, hair = 2, timeMul = 1.3f, tipMul = 0.8f
        },
        new PaxType   // 학생: 요금은 조금 낮고, 리액션은 크지만 사고 감액은 작다
        {
            names = new[] { "학생 미아‖Student Mia", "학생 제이크‖Student Jake", "학생 릴리‖Student Lily" },
            dests = new[] { "도서관‖Public Library", "중앙역‖Grand Central", "시청‖City Hall" },
            hello = new[] { "기사님, {0}까지요! 빨리빨리요!‖Driver, {0}! Hurry hurry!", "{0}까지 가 주세요! 수업 늦었어요!‖{0}, please! I'm late for class!", "{0}! 지각이에요 지각!‖{0}! I'm so late, so late!" },
            react = new[] { "헐 뭐야!! 방금 뭐 쳤어요?‖Whoa, what!! Did you just hit something?", "꺄악!! 기사님!!‖Aaah!! Driver!!", "으아악!! 대박!!‖AAAH!! No way!!" },
            thanks = new[] { "감사합니다! 별점 5점 드릴게요!‖Thank you! Five stars for you!", "덕분에 안 늦었어요! 감사합니다!‖Thanks to you I'm not late! Thank you!" },
            fareMul = 0.9f, hitSense = 0.8f, timeMul = 0.85f, tipMul = 0.5f
        },
        new PaxType   // 아저씨
        {
            names = new[] { "로드리게스 씨‖Mr. Rodriguez", "오브라이언 씨‖Mr. O'Brien", "카터 씨‖Mr. Carter" },
            dests = new[] { "시장‖Farmers Market", "중앙역‖Grand Central", "공원 입구‖Central Park Gate", "시청‖City Hall" },
            hello = new[] { "어이, {0}까지 갑시다.‖Hey, let's go to {0}.", "{0}까지 가요. 막히지 않게 잘 가 봅시다.‖Going to {0}. Let's avoid the traffic.", "택시! {0}까지 부탁해요.‖Taxi! {0}, please." },
            react = new[] { "어허, 운전 조심해!‖Hey, watch it!", "에이, 이 사람아!‖Come on, pal!", "거 운전 좀 살살 합시다!‖Take it easy on the driving!" },
            thanks = new[] { "수고했소.‖Good job.", "잘 왔네. 고마워요.‖Made it. Thanks." },
            fareMul = 1.0f, hitSense = 1.0f
        },
        new PaxType   // 아주머니
        {
            names = new[] { "파텔 부인‖Mrs. Patel", "코왈스키 부인‖Mrs. Kowalski", "존슨 부인‖Mrs. Johnson" },
            dests = new[] { "시장‖Farmers Market", "대학병원‖University Hospital", "도서관‖Public Library", "공원 입구‖Central Park Gate" },
            hello = new[] { "기사님, {0}까지요. 애들 데리러 가야 해요.‖Driver, {0}, please. I have to pick up the kids.", "{0}까지 부탁드려요. 장 본 게 무거워서요.‖{0}, please. My groceries are heavy.", "어휴, 잡았다. {0}요, 천천히 안전하게요.‖Phew, finally got one. {0}, nice and safe, please." },
            react = new[] { "꺄악!! 기사님!!‖Aaah!! Driver!!", "어머, 어머! 조심하세요!!‖Oh my! Oh my! Be careful!!", "방금 사람 쳤어요!?‖Did you just hit someone!?" },
            thanks = new[] { "안전하게 와서 고마워요.‖Thanks for the safe ride.", "고마워요, 기사님. 수고하세요.‖Thank you, driver. Take care." },
            fareMul = 1.0f, hitSense = 1.0f
        },
        new PaxType   // VIP: 요금이 매우 높지만 사고에 아주 민감하다
        {
            names = new[] { "VIP 손님‖VIP Guest", "이사장님‖The Chairman", "관광객 스미스‖Tourist Mr. Smith" },
            dests = new[] { "공항‖JFK Airport", "호텔‖Midtown Hotel", "시청‖City Hall" },
            hello = new[] { "{0}까지 부탁합니다. 아주 부드럽게요.‖{0}, please. Very smoothly.", "{0}까지. 승차감이 중요합니다.‖{0}. Ride comfort matters.", "{0}! Please, smooth ride." },
            react = new[] { "오 마이 갓!!‖Oh my God!!", "이게 무슨 무례한 운전입니까!‖What kind of rude driving is this!", "Stop! 방금 뭐였죠!?‖Stop! What was that just now!?" },
            thanks = new[] { "Excellent! 훌륭한 운전이었어요.‖Excellent! Wonderful driving.", "만족합니다. 수고했어요.‖I'm satisfied. Well done." },
            fareMul = 1.5f, hitSense = 1.7f, weight = 1, timeMul = 0.9f, tipMul = 2f
        },
        new PaxType   // 수다쟁이: 가는 동안 계속 말을 걸고, 대신 팁이 후하다
        {
            names = new[] { "수다쟁이 수‖Sassy Sue", "수다쟁이 채드‖Chatty Chad", "마야 작가님‖Author Maya" },
            dests = new[] { "시장‖Farmers Market", "도서관‖Public Library", "공원 입구‖Central Park Gate", "호텔‖Midtown Hotel" },
            hello = new[] { "기사님 안녕하세요~ {0}까지요! 가면서 얘기나 해요.‖Hi driver~ {0}, please! Let's chat on the way.", "{0}까지 가 주세요! 오늘 날씨 진짜 좋죠?‖{0}, please! Beautiful weather today, right?" },
            react = new[] { "헉, 지금 뭐예요!?‖Gasp, what was that!?", "어머 놀라라!‖Oh my, that startled me!", "기사님 괜찮아요!?‖Are you okay, driver!?" },
            thanks = new[] { "얘기 즐거웠어요! 또 타요~‖Loved the chat! I'll ride again~", "수다 떠느라 시간 가는 줄 몰랐네요!‖I was chatting so much I lost track of time!" },
            chat = new[] { "기사님은 이 일 오래 하셨어요?‖Have you been driving long?", "저번에 탄 택시는 말이 없어서요~‖The last cab I took was so quiet~", "요즘 물가 진짜 올랐죠?‖Prices are crazy these days, right?", "저기 새로 생긴 가게 아세요?‖Do you know that new shop over there?" },
            fareMul = 1.0f, hitSense = 0.9f, tipMul = 1.6f, bigTalk = true
        },
        new PaxType   // 서두르는 손님: 시간이 아주 짧지만 요금과 팁이 높다
        {
            names = new[] { "서두르는 론‖Rushing Ron", "배달원 피트‖Courier Pete", "조 대리‖Assistant Jo" },
            dests = new[] { "공항‖JFK Airport", "중앙역‖Grand Central", "시청‖City Hall" },
            hello = new[] { "{0}까지! 제발 빨리요, 비행기 놓쳐요!‖{0}! Please hurry, I'll miss my flight!", "{0}까지요! 최대한 빨리 부탁합니다!‖{0}! As fast as you possibly can!" },
            react = new[] { "아 뭐예요! 시간 없다고요!‖Hey! I don't have time for this!", "으악, 늦겠어요!‖Aaah, I'm going to be late!", "지금 장난해요!?‖Are you kidding me!?" },
            thanks = new[] { "세이프! 정말 고마워요!‖Safe! Thank you so much!", "덕분에 안 늦었어요!‖Thanks to you I'm not late!" },
            fareMul = 1.3f, hitSense = 1.0f, timeMul = 0.6f, tipMul = 1.5f, weight = 1
        },
        new PaxType   // 멀미하는 손님: 핸들을 급하게 꺾으면 요금이 깎인다
        {
            names = new[] { "창백한 팻‖Pale Pat", "멀미쟁이 퀸‖Queasy Quinn" },
            dests = new[] { "대학병원‖University Hospital", "공원 입구‖Central Park Gate", "도서관‖Public Library" },
            hello = new[] { "{0}까지요… 저 멀미가 심해서 핸들 살살 돌려 주세요…‖{0}… I get really carsick, please turn the wheel gently…", "{0}까지 부탁해요. 급하게 틀면 안 돼요… 우웩.‖{0}, please. No sharp turns… blegh." },
            react = new[] { "우웩… 살살 좀 몰아요…‖Blegh… drive gently…", "으… 속이 울렁거려요…‖Ugh… my stomach is churning…", "핸들 좀 부드럽게요!‖Smoother with the wheel!" },
            thanks = new[] { "살았다… 감사합니다.‖I'm alive… thank you.", "부드럽게 와줘서 고마워요.‖Thanks for the smooth ride." },
            fareMul = 1.1f, hitSense = 1.0f, swerveSense = 0.25f, weight = 1
        },
        new PaxType   // 수상한 손님: 은행까지 태워 가서 기다리게 하고, 나오면 경찰에 쫓긴다 (랜덤 인카운트, 평소에는 안 나옴)
        {
            names = new[] { "선글라스 손님‖Sunglasses Guy", "마스크 손님‖Mask Guy", "큰 가방 손님‖Big Bag Guy" },
            dests = new[] { "은행‖First City Bank" },
            hello = new[]
            {
                "{0}까지 가 주세요. 잠깐 볼일만 보고 올 거예요. 복면은… 패션이에요.‖{0}, please. I'll just be a minute. The mask… it's fashion.",
                "{0}까지요! 가방이 좀 큰데 빈 거예요. 진짜로. 나올 땐 무거워지겠지만.‖{0}! The bag's big but empty. Really. It'll be heavy when I come out, though.",
                "{0}까지 부탁해요. 기사님, 입 무거우시죠? 아, 그냥 물어봤어요.‖{0}, please. You can keep a secret, right driver? Oh, just asking.",
            },
            react = new[] { "으악, 눈에 안 띄게 가자니까요!‖Ugh, I said keep a low profile!", "경찰 오면 어쩌려고요!‖What if the cops show up!", "조용히 좀 몰아요, 조용히!‖Drive quietly, quietly!" },
            thanks = new[] { "(헉헉) 고마워요! 이건 비밀 팁이에요. 우리 만난 적 없죠?‖(panting) Thanks! Here's a secret tip. We've never met, right?", "도주 성공! 이 돈은… 음, 세탁해야겠네요.‖Getaway success! This money… well, needs laundering." },
            fareMul = 1.2f, hitSense = 1.0f, tipMul = 3f, weight = 0, heist = true
        },
        new PaxType   // 폭탄 손님: 속도가 느려지면 가방의 도화선이 탄다. 끝까지 터지지 않으면 "장난감"이라며 요금을 두 배로 준다
        {
            names = new[] { "째깍 손님‖Ticking Tim", "붉은 전선 손님‖Red Wire Rick" },
            dests = new[] { "시청‖City Hall", "공항‖JFK Airport", "중앙역‖Grand Central" },
            hello = new[]
            {
                "{0}까지요. 이 가방에서 째깍째깍 소리가 나는데, 시계예요. 속도가 느려지면 알람이 울릴 거예요. …아마도요.‖{0}. This bag is ticking, but it's a clock. If you slow down the alarm will go off. …Probably.",
                "{0}까지 가 주세요. 절대 속도를 늦추지 마세요. 이유는 묻지 말고요!‖{0}, please. Never slow down. Don't ask why!",
            },
            react = new[] { "으아악 터진다!!‖AAAH it's gonna blow!!", "더 밟아요 더!!‖Faster, faster!!", "도화선이 타요 도화선이!‖The fuse is burning, the fuse!" },
            thanks = new[] { "농담이에요! 장난감이었어요! 놀랐죠? 요금은 두 배로 드릴게요.‖Just kidding! It was a toy! Scared you, huh? I'll pay double.", "이거 폭죽이었어요. 근데 아슬아슬했죠? 헤헤.‖It was a firework. Pretty close call though, right? Heh heh." },
            fareMul = 1.0f, hitSense = 1.0f, tipMul = 1.2f, weight = 1, quirk = Quirk.Bomb
        },
        new PaxType   // 결혼식 커플: 부드럽게 달려야 한다. 급하게 꺾거나 서면 부케가 앞유리로 날아온다
        {
            names = new[] { "신랑 신부‖Bride & Groom", "웨딩 커플‖Wedding Couple" },
            dests = new[] { "호텔‖Midtown Hotel", "공원 입구‖Central Park Gate", "시청‖City Hall" },
            hello = new[]
            {
                "{0}까지요! 결혼식에 늦었어요! 단, 부케가 날아갈 정도로 흔들면 안 돼요!‖{0}! We're late for our wedding! But don't shake us so hard the bouquet flies away!",
                "{0}까지 부탁해요. 오늘이 우리 인생 최고의 날이니까 부드럽게요!‖{0}, please. Today's the best day of our lives, so nice and smooth!",
            },
            react = new[] { "꺄악 부케!!‖Aaah, the bouquet!!", "내 드레스!!‖My dress!!", "여보 괜찮아?! 부케는?!‖Honey, are you okay?! The bouquet?!" },
            thanks = new[] { "결혼해 주셔서… 아니 태워 주셔서 감사해요!‖Thanks for marrying us… I mean, driving us!", "축의금 드릴게요! 안 받으면 서운해요!‖Here's a wedding gift! I'll be hurt if you refuse!" },
            fareMul = 1.2f, hitSense = 1.2f, tipMul = 1.0f, weight = 1, quirk = Quirk.Wedding, hair = 1
        },
        new PaxType   // 외계인 손님: 목적지가 지도에 안 나온다. UFO 빛줄기를 찾아서 그 안에서 멈춰야 한다
        {
            names = new[] { "지구인(자칭)‖Totally Human", "초록 손님‖Green Guest" },
            dests = new[] { "지구 밖 주차장‖Off-World Parking Lot" },
            hello = new[]
            {
                "{0}까지요. 좌표는 지구 기준으로 안 되니 UFO 빛줄기를 따라오세요. 지불은 우리 별 코인으로.‖{0}. The coordinates don't work by Earth's map, so follow the UFO beam. I pay in my planet's coins.",
                "{0}으로 가 주세요. 아, 저는 지구인입니다. 평범한 지구인이에요. 삐빅.‖Take me to {0}. Oh, I am an Earthling. A perfectly ordinary Earthling. Beep.",
            },
            react = new[] { "삐빅! 지구인 운전 미숙!‖Beep! Earthling driving error!", "중력 이상 감지!‖Gravity anomaly detected!", "이 별의 택시는 위험하다!‖Taxis on this planet are dangerous!" },
            thanks = new[] { "이 별의 택시는 훌륭하다. 보상으로 우주 코인을 주겠다.‖Taxis on this planet are excellent. As reward I give space coins.", "다음에는 은하계 요금으로 계산하겠다.‖Next time I will pay in galactic rates." },
            fareMul = 1.5f, hitSense = 1.0f, tipMul = 1.5f, weight = 1, quirk = Quirk.Alien, hair = 2
        },
        new PaxType   // 전직 레이서: 시간이 아주 짧고 빠를수록 요금이 오르지만, 사고가 나면 크게 깎인다
        {
            names = new[] { "전직 레이서‖Ex-Racer Rex", "질주 아저씨‖Speedy Sam" },
            dests = new[] { "공항‖JFK Airport", "서킷 입구‖Speedway Gate", "중앙역‖Grand Central" },
            hello = new[]
            {
                "{0}까지 최고 속도로! 더 밟아요 더! 느리면 내가 내려서 뛸 거예요!‖{0} at top speed! Floor it! If you're slow I'll get out and run!",
                "{0}까지요. 코너는 안쪽으로 파고드는 거 알죠? 시간 없어요, 출발!‖{0}. You know to cut the corners tight, right? No time, go!",
            },
            react = new[] { "그걸 박아요?! 레이서 자존심이!‖You hit that?! My racer's pride!", "그게 운전이에요?!‖You call that driving?!", "사고라니, 말도 안 돼!‖A crash? Unbelievable!" },
            thanks = new[] { "좋은 주행이었어요! 이 팁은 우승 상금이에요!‖Great run! This tip is my winnings!", "포디움 올랐다! 고마워요, 기사님!‖Podium finish! Thanks, driver!" },
            chat = new[] { "더 밟아요 더!!‖Faster, faster!!", "코너는 안쪽으로 파고들라고요!‖Take the corners tight!", "이 정도 속도로 서킷 가면 꼴찌예요!‖At this speed you'd finish last on a track!" },
            fareMul = 1.0f, hitSense = 2.5f, timeMul = 0.55f, tipMul = 3.5f, weight = 1, quirk = Quirk.Racer
        },
        new PaxType   // 욱한 손님: 가끔 핸들을 잡아서 몇 초 동안 조향이 반대로 먹힌다
        {
            names = new[] { "욱한 손님‖Hothead Hank", "성질 급한 손님‖Short-Fuse Frank" },
            dests = new[] { "시장‖Farmers Market", "중앙역‖Grand Central", "대학병원‖University Hospital" },
            hello = new[]
            {
                "{0}까지요! 저 오늘 기분이 별로 안 좋거든요. 운전 잘 부탁해요… 진짜로.‖{0}! I'm in a bad mood today. Drive well, please… I mean it.",
                "{0}까지. 운전 마음에 안 들면 어떻게 되는지 알죠? 농담이에요. 반쯤은요.‖{0}. You know what happens if I don't like your driving? Just kidding. Half kidding.",
            },
            react = new[] { "내가 운전할래!!‖I'll drive!!", "그쪽 말고 이쪽이라고!!‖Not that way, THIS way!!", "비켜 봐요 내가 할게!!‖Move over, I'll do it!!" },
            thanks = new[] { "아까는 미안해요… 팁 드릴게요.‖Sorry about earlier… here's a tip.", "내가 운전한 것보다 낫네요. 인정!‖Better than my driving. I admit it!" },
            chat = new[] { "그쪽 말고 이쪽으로!‖Not that way, this way!", "내가 더 잘해!!‖I can do it better!!" },
            fareMul = 1.0f, hitSense = 1.0f, tipMul = 1.3f, weight = 1, quirk = Quirk.Fighter
        },
    };

    // 은행 강도 인카운트 대사
    public static readonly string[] HeistHideouts = { "비밀 아지트‖Secret Hideout", "이모네 집‖Auntie's Place", "폐공장‖Abandoned Factory", "역 앞 코인락커‖Station Coin Locker" };

    public static readonly string[] HeistReturnLines =
    {
        "(헉헉) 출발! 출발해요!! 경찰이 와요!! {0}까지 빨리빨리!!‖(panting) Go! GO!! The cops are coming!! To {0}, hurry hurry!!",
        "가자 가자 가자!! 뒤돌아보지 마요!! {0}까지요!!‖Go go go!! Don't look back!! To {0}!!",
        "기사님 사랑해요!! 아무튼 {0}까지 전속력으로!!‖I love you, driver!! Anyway, full speed to {0}!!",
    };

    public static readonly string[] HeistWaitLines =
    {
        "금방 올게요! 여기서 꼼짝 말고 기다려 주세요!‖Be right back! Don't move from this spot!",
        "5분이면 돼요. 아, 시동은 끄지 마시고요.‖Five minutes. Oh, and don't turn off the engine.",
    };

    /// <summary>손님 유별 사건 종류(Quirk 상수)에 해당하는 유형. 없으면 null.</summary>
    public static PaxType ByQuirk(int quirk)
    {
        foreach (var t in PaxTypes) if (t.quirk == quirk) return t;
        return null;
    }

    /// <summary>은행 강도 손님 유형.</summary>
    public static PaxType Heist
    {
        get
        {
            foreach (var t in PaxTypes) if (t.heist) return t;
            return PaxTypes[0];
        }
    }

    public static readonly Color[] SkinTones =
    {
        new Color(0.96f, 0.80f, 0.66f), new Color(0.88f, 0.68f, 0.52f), new Color(0.72f, 0.52f, 0.38f), new Color(0.98f, 0.86f, 0.76f)
    };

    public static readonly Color[] HairTones =
    {
        new Color(0.10f, 0.08f, 0.07f), new Color(0.32f, 0.20f, 0.12f), new Color(0.75f, 0.72f, 0.70f), new Color(0.55f, 0.35f, 0.15f)
    };

    public static readonly string[] LateThanks = { "많이 늦었네요… 다음엔 좀 서둘러 주세요.‖That was late… hurry up next time.", "약속 시간에 늦었잖아요. 아쉽네요.‖I missed my appointment. Too bad.", "늦었지만 데려다줘서 고마워요.‖Late, but thanks for getting me here." };

    public static string PickOne(string[] a)
    {
        return Loc.S(a[Random.Range(0, a.Length)]);
    }

    // 손님 이름: 뉴욕이 배경이라 영어 이름을 한글로. 그림이 남자면 남자 이름, 여자면 여자 이름, 외계인은 외계 이름.
    static readonly string[] MaleSets = { "Businessman", "Japanese guy", "Policeman", "Policeman 2", "SWAT", "SWAT 2", "Photographer", "Dealer", "Ambulance", "Bodybuilder", "Robber" };
    static readonly string[] MaleNames = { "존‖John", "마이클‖Michael", "데이비드‖David", "제임스‖James", "로버트‖Robert", "윌리엄‖William", "토니‖Tony", "프랭크‖Frank", "빈센트‖Vincent", "리키‖Ricky", "잭‖Jack", "댄‖Dan", "스티브‖Steve", "조‖Joe", "에디‖Eddie" };
    static readonly string[] FemaleNames = { "제니퍼‖Jennifer", "에밀리‖Emily", "사라‖Sarah", "멜리사‖Melissa", "재닛‖Janet", "로라‖Laura", "샐리‖Sally", "브렌다‖Brenda", "트리샤‖Tricia", "니콜‖Nicole", "메리‖Mary", "린다‖Linda", "캐럴‖Carol", "다이앤‖Diane", "리사‖Lisa" };
    static readonly string[] Surnames = { "스미스‖Smith", "존슨‖Johnson", "브라운‖Brown", "밀러‖Miller", "데이비스‖Davis", "로시‖Rossi", "콜먼‖Coleman", "카터‖Carter", "잭슨‖Jackson", "마르티네즈‖Martinez", "리‖Lee", "오브라이언‖O'Brien" };
    static readonly string[] AlienNames = { "조르그‖Zorg", "블립‖Blip", "자프‖Zap", "쿠르‖Kur", "니블‖Nib" };

    /// <summary>그림이 남자인지 (목소리와 이름용). 그림이 없으면 무작위.</summary>
    public static bool IsMale(PixelSet art)
    {
        if (art == null) return Random.value < 0.5f;
        return System.Array.IndexOf(MaleSets, art.name) >= 0;
    }

    /// <summary>그림(art)에 맞는 이름. 그림이 없으면(외부 에셋이 없을 때) 손님 유형의 예전 이름을 쓴다.</summary>
    public static string NameFor(PixelSet art, PaxType type)
    {
        if (art == null) return PickOne(type.names);
        if (art.cat == "alien") return Loc.T("지구인(자칭) ", "Totally Human ") + Loc.S(AlienNames[Random.Range(0, AlienNames.Length)]);
        bool male = System.Array.IndexOf(MaleSets, art.name) >= 0;
        return Loc.S((male ? MaleNames : FemaleNames)[Random.Range(0, male ? MaleNames.Length : FemaleNames.Length)]) + " " + Loc.S(Surnames[Random.Range(0, Surnames.Length)]);
    }

    // 가중치에 따라 손님 유형을 하나 뽑는다
    public static PaxType PickType()
    {
        int total = 0;
        foreach (var t in PaxTypes) total += Mathf.Max(0, t.weight);
        int roll = Random.Range(0, Mathf.Max(1, total));
        foreach (var t in PaxTypes)
        {
            roll -= Mathf.Max(0, t.weight);
            if (roll < 0) return t;
        }
        return PaxTypes[0];
    }
}
