using UnityEngine;

namespace AD
{
    /// <summary>
    /// 미리 정의하고 사용할 것들
    /// </summary>
    public class Define : MonoBehaviour
    {
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
    }
}