namespace AD
{
    /// <summary>
    /// 게임에서 사용될 상수를 정의하는 클래스
    /// </summary>
    public static class GameConstants
    {
        /// <summary>
        /// Pool에서 가져온 객체 기본으로 담아두는 GameObject의 이름
        /// </summary>
        public const string ActivePool = "ActivePool";

        /// <summary>
        /// 사용 중인 Scene 목록
        /// </summary>
        public enum Scene
        {
            NextScene,
            Login,
            SetCharacter,
            Main,
            Game
        }

        /// <summary>
        /// 모든 객체 (몬스터, 플레이어 등)
        /// </summary>
        public enum Creature
        {
            Player,
            Bat,
            Magma,
            Chest,
            Crab,
            RatAssassin,
            Spider,
            SpiderToxin,
            SpiderKing,
            LizardWarrior,
            Werewolf,
            Beholder,
            FylingDemon
        }

        /// <summary>
        /// 상점 아이템
        /// </summary>
        public enum Item
        {
            None,
            SimpleSword,
            MasterSword,
            SimpleShield,
            MasterShield
        }

        /// <summary>
        /// 인앱 결제 아이템
        /// </summary>
        public enum IAPItem
        {
            ProductNoAds
        }

        /// <summary>
        /// 기타 요소
        /// </summary>
        public enum ETC
        {
            TMPDamage
        }
    }
}
