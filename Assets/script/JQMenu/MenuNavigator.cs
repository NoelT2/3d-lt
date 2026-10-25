using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuNavigator : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public string name;
        public GameObject ui;        // root of this page's UI (can be empty for now)
        public int animatorValue;    // the Animator "Page" value for this page's camera pose
    }

    [SerializeField] private Animator cameraAnimator;
    [SerializeField] private string pageParameter = "Page";
    [SerializeField] private Page[] pages;               // element 0 = the true main menu
    [SerializeField] private GameObject backButton;
    [SerializeField] private float transitionTime = 1f;  // match the Animator transition duration
    [SerializeField] private float fadeTime = 0.25f;

    private readonly Stack<int> history = new Stack<int>();
    private int current;
    private Coroutine routine;

    private void Awake()
    {
        if (backButton != null) backButton.SetActive(false);
        for (int i = 1; i < pages.Length; i++) SetGroup(i, 0f);
    }

    public void Open(int index)            // forward: hook this to menu buttons
    {
        if (index == current) return;
        history.Push(current);
        GoTo(index);
    }

    public void Back()                     // hook this to the Back button
    {
        if (history.Count == 0) return;
        GoTo(history.Pop());
    }

    private void GoTo(int index)
    {
        current = index;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Transition(index));
    }

    private IEnumerator Transition(int index)
    {
        for (int i = 0; i < pages.Length; i++) SetGroup(i, 0f);   // hide everything while moving
        if (backButton != null) backButton.SetActive(false);

        cameraAnimator.enabled = true;
        cameraAnimator.SetInteger(pageParameter, pages[index].animatorValue);

        yield return new WaitForSeconds(transitionTime);

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            SetGroup(index, t / fadeTime);
            yield return null;
        }
        SetGroup(index, 1f);

        if (backButton != null) backButton.SetActive(history.Count > 0);   // hidden on the true main menu
    }

    private void SetGroup(int i, float alpha)
    {
        if (pages[i].ui == null) return;
        var g = pages[i].ui.GetComponent<CanvasGroup>();
        if (g == null) g = pages[i].ui.AddComponent<CanvasGroup>();
        g.alpha = alpha;
        g.interactable = alpha >= 1f;
        g.blocksRaycasts = alpha >= 1f;
    }
}