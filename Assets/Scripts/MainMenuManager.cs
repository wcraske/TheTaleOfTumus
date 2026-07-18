using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    //DLL
    [DllImport("TwoMice")] static extern bool InitializeRawInput(IntPtr hwnd);
    [DllImport("TwoMice")] static extern void HandleRawInput(IntPtr lParam);
    [DllImport("TwoMice")] static extern bool GetMouse1LeftPressed();
    [DllImport("TwoMice")] static extern bool GetMouse2LeftPressed();

    //Win32
    [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWL_WNDPROC = -4;
    private const uint WM_INPUT = 0x00FF;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate newWndProc;
    private IntPtr oldWndProc = IntPtr.Zero;
    private IntPtr hwnd = IntPtr.Zero;
    private bool initialized = false;

    //Inspector
    [Header("Main Menu Buttons")]
    public Button playButton;
    public Button optionsButton;

    [Header("Options Panel")]
    public GameObject optionsPanel;
    public TextMeshProUGUI leftHandLabel;
    public TextMeshProUGUI rightHandLabel;
    public GameObject leftCheckmark;
    public GameObject rightCheckmark;
    public Button resetButton;
    public Button closeButton;
    public TextMeshProUGUI instructionText;

    [Header("Scene")]
    [Tooltip("Build index of the scene to load when Play is pressed")]
    public int nextSceneIndex = 1;

    bool optionsOpen = false;

    enum CalibStep { WaitingForLeft, WaitingForRight, Done }
    CalibStep step = CalibStep.WaitingForLeft;


    void Start()
    {
        if (MouseCalibration.Instance == null)
        {
            var go = new GameObject("MouseCalibration");
            go.AddComponent<MouseCalibration>();
        }

        //hook up buttons
        playButton.onClick.AddListener(OnPlay);
        optionsButton.onClick.AddListener(OnOptions);
        resetButton.onClick.AddListener(OnReset);
        if (closeButton) closeButton.onClick.AddListener(OnOptions);

        //start with panel hidden
        optionsPanel.SetActive(false);

        //hook WndProc
        StartCoroutine(InitializeWithDelay());

        //sync UI
        RefreshCalibrationUI();
    }

    IEnumerator InitializeWithDelay()
    {
        yield return new WaitForSeconds(0.5f);

        hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
            hwnd = FindWindow(null, Application.productName);

        if (hwnd == IntPtr.Zero)
        {
            Debug.LogError("[MainMenu] Could not get window handle.");
            yield break;
        }

        initialized = InitializeRawInput(hwnd);
        if (!initialized)
        {
            Debug.LogError("[MainMenu] Failed to initialize Raw Input.");
            yield break;
        }

        newWndProc = WndProc;
        oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC,
                         Marshal.GetFunctionPointerForDelegate(newWndProc));

        if (oldWndProc == IntPtr.Zero)
            Debug.LogError("[MainMenu] Failed to hook window procedure.");
        else
            Debug.Log("[MainMenu] Raw input ready.");
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_INPUT)
            HandleRawInput(lParam);

        return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
    }

    void OnDestroy()
    {
        if (oldWndProc != IntPtr.Zero && hwnd != IntPtr.Zero)
            SetWindowLongPtr(hwnd, GWL_WNDPROC, oldWndProc);
    }


    void Update()
    {
        if (!optionsOpen || !initialized) return;

        bool m1Click = GetMouse1LeftPressed();
        bool m2Click = GetMouse2LeftPressed();

        if (!m1Click && !m2Click) return;

        int mouseNum;
        if (m1Click)
        {
            mouseNum = 1;
        }
        else
        {
            mouseNum = 2;
        }

        if (step == CalibStep.WaitingForLeft)
        {
            MouseCalibration.Instance.CalibrateLeft(mouseNum);
            step = CalibStep.WaitingForRight;
        }
        else if (step == CalibStep.WaitingForRight)
        {
            if (mouseNum == MouseCalibration.Instance.LeftHandMouse)
            {
                instructionText.text = "That mouse is already your Left Hand!\nClick your RIGHT hand mouse button.";
                return;
            }
            MouseCalibration.Instance.CalibrateRight(mouseNum);
            step = CalibStep.Done;
        }
        else if (step == CalibStep.Done)
        {
            // nothing to do, calibration already complete
        }

        RefreshCalibrationUI();
    }


    void OnPlay()
    {
        SceneManager.LoadScene(nextSceneIndex);
    }

    void OnOptions()
    {
        optionsOpen = !optionsOpen;
        optionsPanel.SetActive(optionsOpen);

        if (optionsOpen)
        {
            if (!MouseCalibration.Instance.LeftCalibrated)
            {
                step = CalibStep.WaitingForLeft;
            }
            else if (!MouseCalibration.Instance.RightCalibrated)
            {
                step = CalibStep.WaitingForRight;
            }
            else
            {
                step = CalibStep.Done;
            }

            RefreshCalibrationUI();
        }
    }

    void OnReset()
    {
        MouseCalibration.Instance.Reset();
        step = CalibStep.WaitingForLeft;
        RefreshCalibrationUI();
    }


    void RefreshCalibrationUI()
    {
        bool leftDone = MouseCalibration.Instance.LeftCalibrated;
        bool rightDone = MouseCalibration.Instance.RightCalibrated;

        leftCheckmark.SetActive(leftDone);
        rightCheckmark.SetActive(rightDone);

        string leftSuffix = "";
        if (leftDone)
        {
            leftSuffix = $"(Mouse {MouseCalibration.Instance.LeftHandMouse})";
        }
        leftHandLabel.text = "Left Hand" + leftSuffix;

        string rightSuffix = "";
        if (rightDone)
        {
            rightSuffix = $"(Mouse {MouseCalibration.Instance.RightHandMouse})";
        }
        rightHandLabel.text = "Right Hand" + rightSuffix;

        if (step == CalibStep.WaitingForLeft)
        {
            instructionText.text = "Click your LEFT hand mouse button.";
        }
        else if (step == CalibStep.WaitingForRight)
        {
            instructionText.text = "Now click your RIGHT hand mouse button.";
        }
        else if (step == CalibStep.Done)
        {
            instructionText.text = "Both hands calibrated! \nYou can close this panel.";
        }
    }
}