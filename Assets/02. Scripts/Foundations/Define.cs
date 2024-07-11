using UnityEngine;

namespace AD
{
    /// <summary>
    /// 미리 정의하고 사용할 것들
    /// </summary>
    public class Define : MonoBehaviour
    {
        /// <summary>
        /// Pool에서 가져온 객체 기본으로 담아두는 go.name
        /// </summary>
        public static string _activePool = "ActivePool";

        /// <summary>
        /// 사용중인 Scene
        /// </summary>
        public enum Scenes
        {
            NextScene,
            Login,
            SetCharacter,
            Main,
            Game
        }

        /// <summary>
        /// 모든 객체
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
    }
}