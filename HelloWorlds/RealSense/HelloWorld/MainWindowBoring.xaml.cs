namespace HelloWorld
{
  using System.Windows;
  using System.Windows.Controls;
  using System.Windows.Media;
  using System.Windows.Shapes;
  public partial class MainWindow : Window
  {
    Rectangle MakeRectangle(PXCMRectI32 rect)
    {
      Rectangle rectangle = new Rectangle()
      {
        Width = ScaleCameraXToCanvasX(rect.w),
        Height = ScaleCameraXToCanvasX(rect.h),
        Stroke = Brushes.Silver
      };
      Canvas.SetLeft(rectangle, ScaleCameraXToCanvasX(rect.x));
      Canvas.SetTop(rectangle, ScaleCameraYToCanvasY(rect.y));
      return (rectangle);
    }
    Ellipse MakeEllipse(PXCMPointF32 point)
    {
      Ellipse ellipse = new Ellipse()
      {
        Width = LANDMARK_ELLIPSE_WIDTH,
        Height = LANDMARK_ELLIPSE_WIDTH,
        Fill = Brushes.Orange
      };
      Canvas.SetLeft(ellipse, ScaleCameraXToCanvasX(point.x) - (LANDMARK_ELLIPSE_WIDTH / 2.0));
      Canvas.SetTop(ellipse, ScaleCameraYToCanvasY(point.y) - (LANDMARK_ELLIPSE_WIDTH / 2.0));
      return (ellipse);
    }
    double ScaleCameraXToCanvasX(double xCamera)
    {
      return ((xCamera / this.imageDimensions.Width) * this.faceCanvas.ActualWidth);
    }
    double ScaleCameraYToCanvasY(double yCamera)
    {
      return ((yCamera / this.imageDimensions.Height) * this.faceCanvas.ActualHeight);
    }
  }
}