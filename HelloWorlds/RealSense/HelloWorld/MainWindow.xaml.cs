namespace HelloWorld
{
  using System.Windows;
  using System.Windows.Media;
  using System.Windows.Media.Imaging;

  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      InitializeComponent();
      this.Loaded += OnLoaded;
    }
    void OnLoaded(object sender, RoutedEventArgs e)
    {
      // Create our 'session' with the camera.
      this.session = PXCMSession.CreateInstance();

      // The SenseManager is really our main object for all our config
      // and functionality.
      this.senseManager = this.session.CreateSenseManager();

      // Switch on the COLOR stream.
      var status = this.senseManager.EnableStream(
        PXCMCapture.StreamType.STREAM_TYPE_COLOR, 0, 0);

      // Switch on and configure face data.
      this.ConfigureFace();

      if (status == pxcmStatus.PXCM_STATUS_NO_ERROR)
      {
        // We can either poll for frames or we can have them pushed at
        // us. Here we take the latter approach and have the frames
        // delivered to us.
        status = this.senseManager.Init(
          new PXCMSenseManager.Handler()
          {
            onNewSample = this.OnNewSample,
            onModuleProcessedFrame = this.OnModuleProcessedFrame
          });

        if (status == pxcmStatus.PXCM_STATUS_NO_ERROR)
        {
          // Set it going - false here means "don't block"
          this.senseManager.StreamFrames(false);
        }
      }
    }
  
    pxcmStatus OnNewSample(int mid, PXCMCapture.Sample sample)
    {
      PXCMImage.ImageData colorImage;
      
      // We get hold of the image here and we keep it until we have a chance
      // to draw it because we are not on the UI thread here. This is inefficient
      // but I think we get away with it here.
      if (sample.color.AcquireAccess(
        PXCMImage.Access.ACCESS_READ,
        PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, 
        out colorImage) == pxcmStatus.PXCM_STATUS_NO_ERROR)
      {
        this.currentColorImage = colorImage;

        if (!this.imageDimensions.HasArea)
        {
          this.imageDimensions.Width = sample.color.info.width;
          this.imageDimensions.Height = sample.color.info.height;
        }
      }
      this.Dispatcher.Invoke(this.DrawColourFrameUIThread);

      sample.color.ReleaseAccess(colorImage);

      return (pxcmStatus.PXCM_STATUS_NO_ERROR);
    }
    void DrawColourFrameUIThread()
    {
      if (this.writeableBitmap == null)
      {
        // Create a bitmap that we can write to.
        this.writeableBitmap = new WriteableBitmap(
          this.imageDimensions.Width,
          this.imageDimensions.Height,
          96,
          96,
          PixelFormats.Bgra32,
          null);

        // Set it as the bitmap source for the image on screen.
        this.colourImage.Source = this.writeableBitmap;
      }
      this.writeableBitmap.WritePixels(
         this.imageDimensions,
         this.currentColorImage.planes[0],
         this.imageDimensions.Width * this.imageDimensions.Height * 4,
         this.imageDimensions.Width * 4);

      this.currentColorImage = null;
    }
    // Bits for display of the colour frame
    PXCMImage.ImageData currentColorImage;
    Int32Rect imageDimensions;
    WriteableBitmap writeableBitmap;

    // Bits relating to the camera
    PXCMSenseManager senseManager;
    PXCMSession session;
  }
}