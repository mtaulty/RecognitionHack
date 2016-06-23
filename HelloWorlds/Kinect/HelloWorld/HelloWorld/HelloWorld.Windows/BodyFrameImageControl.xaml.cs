namespace HelloWorld
{
  using Windows.Foundation;
  using Windows.UI;
  using Windows.UI.Xaml.Controls;
  using Windows.UI.Xaml.Media;
  using Windows.UI.Xaml.Shapes;
  using WindowsPreview.Kinect;

  public partial class BodyFrameImageControl : UserControl
  {
    public BodyFrameImageControl()
    {
      InitializeComponent();
    }
    public void Initialise(FrameDescription colorFrameDescription,
      CoordinateMapper coordinateMapper)
    {
      this.colorFrameDescription = colorFrameDescription;
      this.coordinateMapper = coordinateMapper;
    }
    public void DrawBodies(Body[] bodies)
    {
      // We take the naive approach of getting rid of everything for now in the
      // hope of simplicity. We could do something better. 
      this.canvas.Children.Clear();

      for (int i = 0; i < bodies.Length; i++)
      {
        if (bodies[i].IsTracked)
        {
          this.DrawBody(bodies[i], BodyBrushes[i]);
        }
      }
    }
    void DrawBody(Body body, Brush brush)
    {
      foreach (var entry in body.Joints)
      {
        JointType jointType = entry.Key;
        Joint joint = entry.Value;

        if (joint.TrackingState != TrackingState.NotTracked)
        {
          Point position2d = this.MapPointToCanvasSpace(joint.Position);

          if (!double.IsInfinity(position2d.X) && !double.IsInfinity(position2d.Y))
          {
            Ellipse ellipse =
              this.MakeEllipseForJoint(jointType, joint.TrackingState, brush, position2d);

            this.canvas.Children.Add(ellipse);
          }
        }
      }
    }
    Ellipse MakeEllipseForJoint(
      JointType jointType, 
      TrackingState trackingState, 
      Brush brush, 
      Point position2d)
    {
      int width = jointType == JointType.Head ? HEAD_WIDTH : REGULAR_WIDTH;

      Ellipse ellipse = new Ellipse()
      {
        Width = width,
        Height = width,
        Fill = trackingState == TrackingState.Inferred ? InferredBrush : brush
      };
      Canvas.SetLeft(ellipse, position2d.X - (width / 2));
      Canvas.SetTop(ellipse, position2d.Y - (width / 2));
      return (ellipse);
    }
    Point MapPointToCanvasSpace(CameraSpacePoint point)
    {
      ColorSpacePoint colorSpacePoint = 
        this.coordinateMapper.MapCameraPointToColorSpace(point);

      Point mappedPoint = new Point(
        colorSpacePoint.X / this.colorFrameDescription.Width * this.canvas.ActualWidth,
        colorSpacePoint.Y / this.colorFrameDescription.Height * this.canvas.ActualHeight);

      return (mappedPoint);
    }  
    static readonly int HEAD_WIDTH = 50;
    static readonly int REGULAR_WIDTH = 20;
    static readonly Brush[] BodyBrushes = 
    {
      new SolidColorBrush(Colors.Red),
      new SolidColorBrush(Colors.Green),
      new SolidColorBrush(Colors.Blue),
      new SolidColorBrush(Colors.Brown),
      new SolidColorBrush(Colors.Black),
      new SolidColorBrush(Colors.Orange)
    };
    static readonly Brush InferredBrush = new SolidColorBrush(Colors.Gray);
    FrameDescription colorFrameDescription;
    CoordinateMapper coordinateMapper;

    public static object Brushes { get; private set; }
  }
}