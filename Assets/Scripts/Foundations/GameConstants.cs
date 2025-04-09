namespace AD
{
    /// <summary>
    /// 게임에서 사용될 상수를 정의하는 클래스
    /// </summary>
    public static class GameConstants
    {
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
        public enum Creatures
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
        public enum Items
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
        public enum IAPItems
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
