#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasSelectCharacter : MonoBehaviour
{
    [Header("--- Settings ---")]
    [SerializeField] Transform _tr_male = null;
    [SerializeField] Transform _tr_female = null;
    [SerializeField] Animator _maleAni = null;
    [SerializeField] Animator _femaleAni = null;

    Vector3 _vec_male = Vector3.zero;
    Vector3 _vec_female = Vector3.zero;
    Coroutine _co_move = null;

    #region Functions
    public void ButtonPlay()
    {
        StartCoroutine(Play());
    }

    public void ButtonDirection(string direction)
    {
        if (_co_move != null)
            return;

        SetPosition(direction);

        _co_move = StartCoroutine(Move());
    }

    void SetPosition(string direction)
    {
        if (_tr_male.transform.position.x != 0)
        {
            float x = direction.Equals("Right") ? -5 : 5;
            _tr_male.transform.position = new Vector3(x, _tr_male.position.y, _tr_male.position.z);
            _vec_male = new Vector3(0f, _tr_male.position.y, _tr_male.position.z);
        }
        else
        {
            float x = direction.Equals("Right") ? 5 : -5;
            _vec_male = new Vector3(x, _tr_female.position.y, _tr_female.position.z);
        }

        if (_tr_female.transform.position.x != 0)
        {
            float x = direction.Equals("Right") ? -5 : 5;
            _tr_female.transform.position = new Vector3(x, _tr_female.position.y, _tr_female.position.z);
            _vec_female = new Vector3(0f, _tr_female.position.y, _tr_female.position.z);
        }
        else
        {
            float x = direction.Equals("Right") ? 5 : -5;
            _vec_female = new Vector3(x, _tr_female.position.y, _tr_female.position.z);
        }
    }

    IEnumerator Move()
    {
        while (Mathf.Abs(_tr_male.position.x - _vec_male.x) > 0.01f)
        {
            _tr_male.position = Vector3.Lerp(_tr_male.position, _vec_male, 0.2f);
            _tr_female.position = Vector3.Lerp(_tr_female.position, _vec_female, 0.2f);

            yield return null;
        }
        _tr_male.position = _vec_male;
        _tr_female.position = _vec_female;

        if (_co_move != null)
        {
            StopCoroutine(_co_move);
            _co_move = null;
        }
    }

    IEnumerator Play()
    {
        _maleAni.CrossFade("Select", 0.1f);
        _femaleAni.CrossFade("Select", 0.1f);

        float timer = 0;
        while (timer < 1.2f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        AD.Managers.SceneM.NextScene(AD.Define.Scenes.Main);
    }
    #endregion

#if UNITY_EDITOR
    [CustomEditor(typeof(CanvasSelectCharacter))]
    public class customEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("CanvasSelectCharacter관련 ", MessageType.Info);

            base.OnInspectorGUI();
        }
    }
#endif
}
