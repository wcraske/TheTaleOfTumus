using UnityEngine;

public class MouseCalibration : MonoBehaviour
{
    public static MouseCalibration Instance { get; private set; }

    // 0 = uncalibrated, 1 = mouse1, 2 = mouse2
    public int LeftHandMouse  { get; private set; } = 0;
    public int RightHandMouse { get; private set; } = 0;

    public bool LeftCalibrated  => LeftHandMouse  != 0;
    public bool RightCalibrated => RightHandMouse != 0;
    public bool FullyCalibrated => LeftCalibrated && RightCalibrated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    ///Call when the user clicks their left-hand mouse button during calibration.
    public void CalibrateLeft(int mouseNumber)
    {
        LeftHandMouse = mouseNumber;
        // If both hands were set to the same mouse, clear the right hand
        if (RightHandMouse == mouseNumber) RightHandMouse = 0;
    }

    /// Call when the user clicks their right-hand mouse button during calibration.
    public void CalibrateRight(int mouseNumber)
    {
        RightHandMouse = mouseNumber;
        if (LeftHandMouse == mouseNumber) LeftHandMouse = 0;
    }

    public void Reset()
    {
        LeftHandMouse  = 0;
        RightHandMouse = 0;
    }
}