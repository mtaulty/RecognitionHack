namespace HelloWorld
{
  using System.Linq;
  using System.Windows;
  public partial class MainWindow : Window
  {
    void ConfigureFace()
    {
      // We configure by switching on the face module.
      this.senseManager.EnableFace();

      // Now, grab that module.
      var faceModule = this.senseManager.QueryFace();

      // Configure it...
      using (var config = faceModule.CreateActiveConfiguration())
      {
        // We want face detection. Only doing 1 face for now.
        config.detection.isEnabled = true;
        config.detection.maxTrackedFaces = 1;

        // We want face landmarks. Only doing 1 face for now.
        config.landmarks.isEnabled = true;
        config.landmarks.maxTrackedFaces = 1;

        // We could easily switch on pulse detection and expressions
        // but I want to keep this code short.

        config.ApplyChanges();
      }
      this.faceData = faceModule.CreateOutput();
      faceModule.Dispose();
    }
    pxcmStatus OnModuleProcessedFrame(
      int mid,
      PXCMBase module,
      PXCMCapture.Sample sample)
    {
      // is it our module?
      if (mid == PXCMFaceModule.CUID)
      {
        this.faceData.Update();

        // Any faces?
        var firstFace = this.faceData.QueryFaces().FirstOrDefault();

        if (firstFace != null)
        {
          // face detection - the bounding rectangle of the face.
          var localFaceBox = default(PXCMRectI32);

          if (firstFace.QueryDetection()?.QueryBoundingRect(out localFaceBox) == true)
          {
            this.faceBox = localFaceBox;
          }
          var landmarks = firstFace.QueryLandmarks()?.QueryPoints(
            out this.landmarks);
        }
        else
        {
          this.faceBox = null;
          this.landmarks = null;
        }
      }
      this.Dispatcher.Invoke(this.DrawFaceFrameUIThread);

      return (pxcmStatus.PXCM_STATUS_NO_ERROR);
    }
    void DrawFaceFrameUIThread()
    {
      // Very wasteful, we clear everything every time.
      this.faceCanvas.Children.Clear();

      // Draw a box around the face.
      if (this.faceBox.HasValue)
      {
        this.faceCanvas.Children.Add(this.MakeRectangle(this.faceBox.Value));
      }

      // Draw circles for each of the facial landmarks.
      if (this.landmarks != null)
      {
        foreach (var landmark in this.landmarks)
        {
          this.faceCanvas.Children.Add(this.MakeEllipse(landmark.image));
        }
      }
    }
    // Bits relating to the facial data
    PXCMFaceData.LandmarkPoint[] landmarks;
    PXCMRectI32? faceBox;
    PXCMFaceData faceData;
    const int LANDMARK_ELLIPSE_WIDTH = 5;
  }
}