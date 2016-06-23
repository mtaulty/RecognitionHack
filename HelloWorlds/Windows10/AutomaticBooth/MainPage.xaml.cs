namespace AutomaticBooth
{
  using Microsoft.ProjectOxford.Emotion;
  using Microsoft.ProjectOxford.Face;
  using PhotoControlLibrary;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Reflection;
  using System.Threading.Tasks;
  using Windows.Foundation;
  using Windows.Graphics.Imaging;
  using Windows.Media.FaceAnalysis;
  using Windows.Media.SpeechRecognition;
  using Windows.Media.SpeechSynthesis;
  using Windows.Storage;
  using Windows.UI;
  using Windows.UI.Input.Inking;
  using Windows.UI.Xaml.Controls;
  using Windows.UI.Xaml.Input;
  using Windows.UI.Xaml.Media;

  public sealed partial class MainPage : Page, IPhotoControlHandler
  {
    public MainPage()
    {
      this.InitializeComponent();
      this.Loaded += OnLoaded;
    }
    #region FACE BITS
    public async Task<Rect?> ProcessCameraFrameAsync(SoftwareBitmap bitmap)
    {
      if (this.faceDetector == null)
      {
        this.faceDetector = await FaceDetector.CreateAsync();
      }
      var result = await this.faceDetector.DetectFacesAsync(bitmap);

      this.photoControl.Switch(result?.Count > 0);

      Rect? returnValue = null;

      if (result?.Count > 0)
      {
        returnValue = new Rect(
          (double)result[0].FaceBox.X / bitmap.PixelWidth,
          (double)result[0].FaceBox.Y / bitmap.PixelHeight,
          (double)result[0].FaceBox.Width / bitmap.PixelWidth,
          (double)result[0].FaceBox.Height / bitmap.PixelHeight);
      }
      return (returnValue);
    }
    #endregion // FACE BITS 

    #region SPEECH BITS
    async Task StartListeningForCheeseAsync()
    {
      await this.StartListeningForConstraintAsync(
        new SpeechRecognitionListConstraint(new string[] { "cheese" }));
    }
    async Task StartListeningForFiltersAsync()
    {
      var grammarFile =
        await StorageFile.GetFileFromApplicationUriAsync(
          new Uri("ms-appx:///grammar.xml"));

      await this.StartListeningForConstraintAsync(
        new SpeechRecognitionGrammarFileConstraint(grammarFile));
    }
    async Task StartListeningForConstraintAsync(
      ISpeechRecognitionConstraint constraint)
    {
      if (this.speechRecognizer == null)
      {
        this.speechRecognizer = new SpeechRecognizer();

        this.speechRecognizer.ContinuousRecognitionSession.ResultGenerated
          += OnSpeechResult;
      }
      else
      {
        await this.speechRecognizer.ContinuousRecognitionSession.StopAsync();
      }
      this.speechRecognizer.Constraints.Clear();

      this.speechRecognizer.Constraints.Add(constraint);

      await this.speechRecognizer.CompileConstraintsAsync();

      await this.speechRecognizer.ContinuousRecognitionSession.StartAsync();
    }
    async void OnSpeechResult(
      SpeechContinuousRecognitionSession sender,
      SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
      if ((args.Result.Confidence == SpeechRecognitionConfidence.High) ||
          (args.Result.Confidence == SpeechRecognitionConfidence.Medium))
      {
        if (args.Result?.RulePath?.FirstOrDefault() == "filter")
        {
          var filter =
            args.Result.SemanticInterpretation.Properties["emotion"].FirstOrDefault();

          if (!string.IsNullOrEmpty(filter))
          {
            await this.Dispatcher.RunAsync(
              Windows.UI.Core.CoreDispatcherPriority.Normal,
              async () =>
              {
                await this.photoControl.ShowFilteredGridAsync(filter);
              }
            );
          }
        }
        else if (args.Result.Text.ToLower() == "cheese")
        {
          await this.Dispatcher.RunAsync(
            Windows.UI.Core.CoreDispatcherPriority.Normal,
            async () =>
            {
              var photoResult = await this.photoControl.TakePhotoAsync();

              if (photoResult != null)
              {
                await this.AddFaceBasedTagsToPhotoAsync(photoResult);
                await this.AddEmotionBasedTagsToPhotoAsync(photoResult);
                await this.SpeakAsync("That's lovely, you look great!");
              }
            }
          );
        }
      }
    }
    async Task SpeakAsync(string text)
    {
      // Note, the assumption here is very much that we speak one piece of
      // text at a time rather than have multiple in flight - that needs
      // a different solution (with a queue).
      await Dispatcher.RunAsync(
        Windows.UI.Core.CoreDispatcherPriority.Normal,
        async () =>
        {
          // Create the synthesizer if we need to.
          if (this.speechSynthesizer == null)
          {
            // Easy create, just choosing first female voice.
            this.speechSynthesizer = new SpeechSynthesizer()
            {
              Voice = SpeechSynthesizer.AllVoices.Where(
                v => v.Gender == VoiceGender.Female).First()
            };

            // Make a media element to play the speech.
            this.mediaElementForSpeech = new MediaElement();

            // When the media ends, get rid of stream.
            this.mediaElementForSpeech.MediaEnded += (s, e) =>
            {
              this.speechMediaStream?.Dispose();
              this.speechMediaStream = null;
            };
          }
          // Now, turn the text into speech.
          this.speechMediaStream =
            await this.speechSynthesizer.SynthesizeTextToStreamAsync(text);

          this.mediaElementForSpeech.SetSource(this.speechMediaStream, string.Empty);

          // Speak it.
          this.mediaElementForSpeech.Play();
        }
      );
    }
    #endregion // SPEECH BITS

    #region INKING BITS
    void AddLayerForManipulations()
    {
      this.inkOverlay = new InkCanvas()
      {
        ManipulationMode =
          ManipulationModes.Rotate |
          ManipulationModes.Scale
      };

      this.inkOverlay.ManipulationDelta += OnManipulationDelta;

      var presentation = this.inkOverlay.InkPresenter.CopyDefaultDrawingAttributes();
      presentation.Color = Colors.Yellow;
      presentation.Size = new Size(2, 2);
      this.inkOverlay.InkPresenter.UpdateDefaultDrawingAttributes(presentation);

      this.inkOverlay.InkPresenter.StrokesCollected += OnStrokesCollected;

      this.photoControl.AddOverlayToDisplayedPhoto(this.inkOverlay);
    }

    async void OnStrokesCollected(
      InkPresenter sender,
      InkStrokesCollectedEventArgs args)
    {
      // create the ink recognizer if we haven't done already.
      if (this.inkRecognizer == null)
      {
        this.inkRecognizer = new InkRecognizerContainer();
      }
      // recognise the ink which has not already been recognised
      // (i.e. do incremental ink recognition).
      var results = await this.inkRecognizer.RecognizeAsync(
        sender.StrokeContainer,
        InkRecognitionTarget.Recent);

      // update the container so that it knows next time that this
      // ink is already recognised.
      sender.StrokeContainer.UpdateRecognitionResults(results);

      // we take all the top results that the recogniser gives us
      // back.
      var newTags = results.Select(
        result => result.GetTextCandidates().FirstOrDefault());

      // add the new tags to our photo.
      await this.photoControl.AddTagsToPhotoAsync(this.currentPhotoId, newTags);
    }
    void RemoveLayerForManipulations()
    {
      this.inkOverlay.ManipulationDelta -= this.OnManipulationDelta;
      this.photoControl.RemoveOverlayFromDisplayedPhoto(this.inkOverlay);
      this.inkOverlay = null;
    }
    async Task SaveInkToFileAsync()
    {
      if (this.inkOverlay.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
      {
        var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
          this.InkStorageFileName, CreationCollisionOption.ReplaceExisting);

        using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
          await this.inkOverlay.InkPresenter.StrokeContainer.SaveAsync(stream);
        }
      }
    }
    async Task LoadInkFromFileAsync()
    {
      try
      {
        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(
          this.InkStorageFileName);

        using (var stream = await file.OpenReadAsync())
        {
          await this.inkOverlay.InkPresenter.StrokeContainer.LoadAsync(stream);
        }
      }
      catch (FileNotFoundException)
      {

      }
    }
    #endregion // INKING BITS

    #region OTHER BITS
    async void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
    {
      await this.photoControl.InitialiseAsync(this);

      var filter = ((App)App.Current).WaitingFilter;

      if (!string.IsNullOrEmpty(filter))
      {
        await this.photoControl.ShowFilteredGridAsync(filter);
      }
    }
    public async Task<bool> AuthoriseUseAsync()
    {
      return (true);
    }
    void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
      this.photoControl.UpdatePhotoTransform(e.Delta);
    }
    public async Task OnModeChangedAsync(PhotoControlMode newMode)
    {
      switch (newMode)
      {
        case PhotoControlMode.Unauthorised:
          break;
        case PhotoControlMode.Grid:
          await this.StartListeningForFiltersAsync();
          break;
        case PhotoControlMode.Capture:
          await this.StartListeningForCheeseAsync();
          break;
        default:
          break;
      }
    }
    async Task AddEmotionBasedTagsToPhotoAsync(PhotoResult photoResult)
    {
      // See comment at bottom of file
      if (!string.IsNullOrEmpty(cognitiveServiceEmotionServiceKey))
      {
        EmotionServiceClient client = new EmotionServiceClient(
          cognitiveServiceEmotionServiceKey);

        // Open the photo file we just captured.
        using (var stream = await photoResult.PhotoFile.OpenStreamForReadAsync())
        {
          // Call the cloud looking for emotions.
          var results = await client.RecognizeAsync(stream);

          // We're only taking the first result here.
          var scores = results?.FirstOrDefault()?.Scores;

          if (scores != null)
          {
            // This object has properties called Sadness, Happiness,
            // Fear, etc. all with floating point values 0..1
            var publicProperties = scores.GetType().GetRuntimeProperties();

            // We'll have any property with a score > 0.5f.
            var automaticTags =
              publicProperties
                .Where(
                  property => (float)property.GetValue(scores) > 0.5)
                .Select(
                  property => property.Name)
                .ToList();

            if (automaticTags.Count > 0)
            {
              // Add them to our photo!
              await this.photoControl.AddTagsToPhotoAsync(
                photoResult.PhotoId,
                automaticTags);
            }
          }
        }
      }
    }
    async Task AddFaceBasedTagsToPhotoAsync(PhotoResult photoResult)
    {
      // See comment at bottom of file.
      if (!string.IsNullOrEmpty(cognitiveServiceFaceServiceKey))
      {
        FaceServiceClient client = new FaceServiceClient(
          cognitiveServiceFaceServiceKey);

        using (var stream = await photoResult.PhotoFile.OpenStreamForReadAsync())
        {
          var attributes = new FaceAttributeType[]
          {
          FaceAttributeType.Age,
          FaceAttributeType.FacialHair,
          FaceAttributeType.Gender,
          FaceAttributeType.Glasses,
          FaceAttributeType.Smile
          };
          var results = await client.DetectAsync(stream, true, false, attributes);

          var firstFace = results?.FirstOrDefault();

          if (firstFace != null)
          {
            var automaticTags = new List<string>();
            automaticTags.Add($"age {firstFace.FaceAttributes.Age}");
            automaticTags.Add(firstFace.FaceAttributes.Gender.ToString());
            automaticTags.Add(firstFace.FaceAttributes.Glasses.ToString());

            Action<double, string> compareFunc =
              (double value, string name) =>
              {
                if (value > 0.5) automaticTags.Add(name);
              };

            compareFunc(firstFace.FaceAttributes.Smile, "smile");
            compareFunc(firstFace.FaceAttributes.FacialHair.Beard, "beard");
            compareFunc(firstFace.FaceAttributes.FacialHair.Moustache, "moustache");
            compareFunc(firstFace.FaceAttributes.FacialHair.Sideburns, "sideburns");

            await this.photoControl.AddTagsToPhotoAsync(
              photoResult.PhotoId, automaticTags);
          }
        }
      }
    }
    public async Task OnOpeningPhotoAsync(Guid photo)
    {
      this.currentPhotoId = photo;
      this.AddLayerForManipulations();
      await this.LoadInkFromFileAsync();
    }
    public async Task OnClosingPhotoAsync(Guid photo)
    {
      await this.SaveInkToFileAsync();
      this.RemoveLayerForManipulations();
    }
    #endregion // OTHER BITS

    FaceDetector faceDetector;
    SpeechSynthesisStream speechMediaStream;
    MediaElement mediaElementForSpeech;
    SpeechSynthesizer speechSynthesizer;
    SpeechRecognizer speechRecognizer;
    Guid currentPhotoId;
    InkCanvas inkOverlay;
    InkRecognizerContainer inkRecognizer;
    string InkStorageFileName => $"{this.currentPhotoId}.ink";

    // If you want the extra pieces of code around CognitiveServices to do
    // something then plug your API keys for the emotion service and the
    // facial service in here.
    static readonly string cognitiveServiceEmotionServiceKey = string.Empty;
    static readonly string cognitiveServiceFaceServiceKey = string.Empty;
  }
}
