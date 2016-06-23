namespace HelloWorld
{
  using Windows.UI.Xaml.Controls;
  using WindowsPreview.Kinect;

  public sealed partial class MainPage : Page
  {
    public MainPage()
    {
      this.InitializeComponent();

      this.Loaded += OnLoaded;
    }
    void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      this.sensor = KinectSensor.GetDefault();
      this.sensor.Open();

      this.bodyControl.Initialise(
        this.sensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra),
        this.sensor.CoordinateMapper);

      this.bodies = new Body[this.sensor.BodyFrameSource.BodyCount];

      this.reader = this.sensor.BodyFrameSource.OpenReader();

      this.reader.FrameArrived += (source, args) =>
        {
          if (args.FrameReference != null)
          {
            using (var frame = args.FrameReference.AcquireFrame())
            {
              if (frame != null)
              {
                frame.GetAndRefreshBodyData(this.bodies);
                this.bodyControl.DrawBodies(this.bodies);
              }
            }
          }
        };
    }
    Body[] bodies;
    KinectSensor sensor;
    BodyFrameReader reader;
  }
}
