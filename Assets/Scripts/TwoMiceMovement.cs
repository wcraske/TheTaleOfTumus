using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class TwoMiceMovement : MonoBehaviour
{
    //DLL
    [DllImport("TwoMice")] static extern bool InitializeRawInput(IntPtr hwnd);
    [DllImport("TwoMice")] static extern void HandleRawInput(IntPtr lParam);
    [DllImport("TwoMice")] static extern void GetMouse1Delta(out int dx, out int dy);
    [DllImport("TwoMice")] static extern void GetMouse2Delta(out int dx, out int dy);

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

    [Header("tumus")]
    public Transform tumus;

    //Limb refs
    [Header("Right Limb")]
    public Transform rightBicep;
    public Transform rightForearm;
    public Transform rightHand; 
    [Header("Left Limb")]
    public Transform leftBicep;
    public Transform leftForearm;
    public Transform leftHand;

    private float rightBicepLength;
    private float rightForearmLength;

    private float leftBicepLength;
    private float leftForearmLength;

    private float camDist = 10f; //distance from camera to arms, used for screen to world conversion

    //IK tuning 
    [Header("IK Tuning")]
    [Tooltip("Rotation offset (degrees) to correct for how the bicep sprite is drawn. Tune visually.")]
    public float spriteAngleOffsetBicep = 0f;
    [Tooltip("Rotation offset (degrees) to correct for how the forearm sprite is drawn. Tune visually.")]
    public float spriteAngleOffsetForearm = 0f;
    [Tooltip("Flip this if the elbow bends the wrong way.")]
    public bool bendPositive = true;

    //Virtual cursor 
    [Header("Cursor Tuning")]
    public float rightSensitivity = 4f;
    public float leftSensitivity = 4f;
    private Vector2 rightVirtualCursorPos;
    private Vector2 leftVirtualCursorPos;
    private int rightMouseId; // 1 or 2, resolved from calibration
    private int leftMouseId; 

    //debug shit
    //[Header("Debug Cursor Markers")]
    //public Transform rightCursorMarker;
    //public Transform leftCursorMarker;


    void Start()
    {
        calcForearmBicepLength();
        if (MouseCalibration.Instance == null)
        {
            Debug.LogWarning("No calibration found, defaulting right hand to mouse 1");
            rightMouseId = 1;
            leftMouseId = 2;
        }
        else if (!MouseCalibration.Instance.RightCalibrated)
        {
            Debug.LogWarning("Right mouse not calibrated, defaulting to mouse 1");
            rightMouseId = 1;
            leftMouseId = 2;
        }
        else
        {
            rightMouseId = MouseCalibration.Instance.RightHandMouse;
            leftMouseId = MouseCalibration.Instance.LeftHandMouse;
        }

        //DEBUG: confirm what got resolved before anything else runs
        Debug.Log($"[TwoMiceMovement] DEBUG resolved rightMouseId={rightMouseId}, leftMouseId={leftMouseId}");

        //possible to find the location of the object for hand and grab those coords?
        rightVirtualCursorPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
        leftVirtualCursorPos = new Vector2(Screen.width / 2f, Screen.height / 2f);

        InitInput();
    }

    void InitInput()
    {
        hwnd = GetActiveWindow();
        if (hwnd == IntPtr.Zero)
            hwnd = FindWindow(null, Application.productName);

        if (hwnd == IntPtr.Zero)
        {
            Debug.LogError("[TwoMiceMovement] Could not get window handle.");
            return;
        }

        initialized = InitializeRawInput(hwnd);
        if (!initialized)
        {
            Debug.LogError("[TwoMiceMovement] Failed to initialize Raw Input.");
            return;
        }

        newWndProc = WndProc;
        oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC,
                         Marshal.GetFunctionPointerForDelegate(newWndProc));

        if (oldWndProc == IntPtr.Zero)
            Debug.LogError("[TwoMiceMovement] Failed to hook window procedure.");
        else
            Debug.Log("[TwoMiceMovement] Ready.");
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

    void calcForearmBicepLength()
    {
        leftBicepLength = Vector2.Distance(leftBicep.position, leftForearm.position);
        leftForearmLength = Vector2.Distance(leftForearm.position, leftHand.position);
        rightBicepLength = Vector2.Distance(rightBicep.position, rightForearm.position);
        rightForearmLength = Vector2.Distance(rightForearm.position, rightHand.position);
    }

    void LateUpdate()
    {
        if (!initialized) return;

        int dxr = 0;
        int dyr = 0;
        int dxl = 0;
        int dyl = 0;

        if (rightMouseId == 1){
             GetMouse1Delta(out dxr, out dyr);
        }
        else{
            GetMouse2Delta(out dxr, out dyr);
        }
        if (leftMouseId == 1){
             GetMouse1Delta(out dxl, out dyl);
        }
        else{
            GetMouse2Delta(out dxl, out dyl);
        }

        //DEBUG: print raw deltas each frame so we can see which physical mouse is actually feeding which arm
        if (dxr != 0 || dyr != 0 || dxl != 0 || dyl != 0)
        {
            Debug.Log($"[TwoMiceMovement] DEBUG rightMouseId={rightMouseId} dxr={dxr} dyr={dyr} | leftMouseId={leftMouseId} dxl={dxl} dyl={dyl}");
        }

        rightVirtualCursorPos += new Vector2(-dxr, dyr) * rightSensitivity; 
        rightVirtualCursorPos.x = Mathf.Clamp(rightVirtualCursorPos.x, 0, Screen.width);
        rightVirtualCursorPos.y = Mathf.Clamp(rightVirtualCursorPos.y, 0, Screen.height);

        Vector3 rightWorldTarget = Camera.main.ScreenToWorldPoint(new Vector3(rightVirtualCursorPos.x, rightVirtualCursorPos.y, camDist));

        leftVirtualCursorPos += new Vector2(-dxl, dyl) * leftSensitivity; 
        leftVirtualCursorPos.x = Mathf.Clamp(leftVirtualCursorPos.x, 0, Screen.width);
        leftVirtualCursorPos.y = Mathf.Clamp(leftVirtualCursorPos.y, 0, Screen.height);

        Vector3 leftWorldTarget = Camera.main.ScreenToWorldPoint(new Vector3(leftVirtualCursorPos.x, leftVirtualCursorPos.y, camDist));

        SolveIK(leftWorldTarget, 0);
        SolveIK(rightWorldTarget, 1);
        //debug shit
        //if (rightCursorMarker != null) rightCursorMarker.position = rightWorldTarget;
        //if (leftCursorMarker != null) leftCursorMarker.position = leftWorldTarget;
        inchingMovement();
    }



    //solve for right and lef
    void SolveIK(Vector2 target, int limbID)
    {
        if(limbID == 0)
        //left limb
        {
            Vector2 shoulderPos = leftBicep.position;
            Vector2 toTarget = target - shoulderPos;
            float distToTarget = toTarget.magnitude;

            float maxReach = leftBicepLength + leftForearmLength - 0.001f;
            float minReach = Mathf.Abs(leftBicepLength - leftForearmLength) + 0.001f;
            distToTarget = Mathf.Clamp(distToTarget, minReach, maxReach);

            float cosElbow = (leftBicepLength * leftBicepLength + leftForearmLength * leftForearmLength - distToTarget * distToTarget) / (2f * leftBicepLength * leftForearmLength);
            cosElbow = Mathf.Clamp(cosElbow, -1f, 1f);
            float elbowAngleRad = Mathf.Acos(cosElbow); // interior angle at elbow

            float cosShoulder = (leftBicepLength * leftBicepLength + distToTarget * distToTarget - leftForearmLength * leftForearmLength) / (2f * leftBicepLength * distToTarget);
            cosShoulder = Mathf.Clamp(cosShoulder, -1f, 1f);
            float shoulderOffsetRad = Mathf.Acos(cosShoulder);

            float baseAngleRad = Mathf.Atan2(toTarget.y, toTarget.x);
            float bendSign;
            if (bendPositive)
            {
                bendSign = 1f;
            }
            else
            {
                bendSign = -1f;
            }

            float shoulderAngleDeg = (baseAngleRad + bendSign * shoulderOffsetRad) * Mathf.Rad2Deg;
            leftBicep.rotation = Quaternion.Euler(0, 0, shoulderAngleDeg + spriteAngleOffsetBicep);

            float elbowAngleDeg = elbowAngleRad * Mathf.Rad2Deg;
            float forearmLocalAngle = bendSign * (180f - elbowAngleDeg);
            leftForearm.localRotation = Quaternion.Euler(0, 0, forearmLocalAngle + spriteAngleOffsetForearm);

        }
        
        if(limbID == 1)
        {
            Vector2 shoulderPos = rightBicep.position;
            Vector2 toTarget = target - shoulderPos;
            float distToTarget = toTarget.magnitude;

            float maxReach = rightBicepLength + rightForearmLength - 0.001f;
            float minReach = Mathf.Abs(rightBicepLength - rightForearmLength) + 0.001f;
            distToTarget = Mathf.Clamp(distToTarget, minReach, maxReach);

            float cosElbow = (rightBicepLength * rightBicepLength + rightForearmLength * rightForearmLength - distToTarget * distToTarget) / (2f * rightBicepLength * rightForearmLength);
            cosElbow = Mathf.Clamp(cosElbow, -1f, 1f);
            float elbowAngleRad = -Mathf.Acos(cosElbow); // interior angle at elbow

            float cosShoulder = (rightBicepLength * rightBicepLength + distToTarget * distToTarget - rightForearmLength * rightForearmLength) / (2f * rightBicepLength * distToTarget);
            cosShoulder = Mathf.Clamp(cosShoulder, -1f, 1f);
            float shoulderOffsetRad = Mathf.Acos(cosShoulder);

            float baseAngleRad = Mathf.Atan2(toTarget.y, toTarget.x);
            float bendSign;
            if (bendPositive)
            {
                bendSign = 1f;
            }
            else
            {
                bendSign = -1f;
            }

            float shoulderAngleDeg = (baseAngleRad + bendSign * shoulderOffsetRad) * Mathf.Rad2Deg;
            rightBicep.rotation = Quaternion.Euler(0, 0, shoulderAngleDeg + spriteAngleOffsetBicep);

            float elbowAngleDeg = elbowAngleRad * Mathf.Rad2Deg;
            float forearmLocalAngle = bendSign * (180f - elbowAngleDeg);
            rightForearm.localRotation = Quaternion.Euler(0, 0, forearmLocalAngle + spriteAngleOffsetForearm);

        }
    }

    void inchingMovement()
    {
        float leftBicepAngle = Mathf.DeltaAngle(0, leftBicep.eulerAngles.z);
        float rightBicepAngle = Mathf.DeltaAngle(0, rightBicep.eulerAngles.z);

        if (rightBicepAngle > -100 && rightBicepAngle < -80 && leftBicepAngle > -100 && leftBicepAngle < -80)
        {
            tumus.position += Vector3.right * 0.005f;
        }

        if (rightBicepAngle > 80 && rightBicepAngle < 100 && leftBicepAngle > 80 && leftBicepAngle < 100)
        {
            tumus.position += Vector3.left * 0.005f;
        }
    }


}