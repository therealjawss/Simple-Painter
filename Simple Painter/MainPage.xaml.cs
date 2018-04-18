using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas;
using Windows.UI.Core;
using Windows.Storage.Streams;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Simple_Painter
{

    
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private InkSynchronizer _inkSynchronizer;
        private bool _isErasing;
        private Point _lastpoint;
        private bool _isPencil = false;
      //  private readonly List<InkStrokeContainer> _pencilStrokes = new List<InkStrokeContainer>();
       // private readonly List<InkStrokeContainer> _otherStrokes = new List<InkStrokeContainer>();
        private readonly Stack<InkStrokeContainer> _erasedStrokes = new Stack<InkStrokeContainer>();
        private readonly Stack<int> _erasedIndices = new Stack<int>();
        private readonly List<InkStrokeContainer> _inkStrokes = new List<InkStrokeContainer>();

        private Stack<Object> history = new Stack<Object>();
        private Flyout flyout;
        private Flyout _eraseAllFlyout; 

        public MainPage()
        {
            this.InitializeComponent();
            Loaded += Page_Loaded;
        }

      
        
       

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var inkPresenter = InkCanvas.InkPresenter;
            inkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse  | CoreInputDeviceTypes.Pen;

            btnOpen.Click += BtnOpen_Click;
            btnSave.Click += BtnSave_Click;
            btnUndo.Click += BtnUndo_Click;
            _inkSynchronizer = inkPresenter.ActivateCustomDrying();

            inkPresenter.StrokesCollected += InkPresenter_StrokesCollected;

            
            var eraser = InkToolbar.GetToolButton(InkToolbarTool.Eraser) as InkToolbarEraserButton;
            if (eraser !=null)
            {
                eraser.Checked += Eraser_Checked;
                eraser.Unchecked += Eraser_Unchecked;
            }

            InkCanvas.InkPresenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;

            var unprocessedInput = InkCanvas.InkPresenter.UnprocessedInput;
            unprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            unprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            unprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
            unprocessedInput.PointerExited += UnprocessedInput_PointerExited;
            unprocessedInput.PointerLost += UnprocessedInput_PointerLost;

            //flyout = FlyoutBase.GetAttachedFlyout(eraser) as Flyout;
            _eraseAllFlyout = FlyoutBase.GetAttachedFlyout(eraser) as Flyout;

            if (_eraseAllFlyout != null)
            {
                var panel = _eraseAllFlyout.Content as StackPanel;
                
                var button = panel.Children[3] as InkToolbarFlyoutItem;
                panel.Children.RemoveAt(3);
                //panel.Children.OfType<InkToolbarFlyoutItem>().FirstOrDefault();
                if (button != null)
                {
                    var newButton = new InkToolbarFlyoutItem();
                    newButton.Style = button.Style;
                    newButton.Content = new TextBlock() {  Text="Erase all ink"};
                    //   newButton.Content = button.Content;
                    newButton.Name = button.Name;
                    newButton.Click += EraseAllInk;
                    try {
                        //  panel.Children.Remove(button);
                        //  panel.Children.Add(newButton);
                        panel.Children.Add(newButton);
                    }
                    catch (Exception ex) {
                        System.Diagnostics.Debug.Write(ex);
                    }
                    
                }

            }

     
        }

  
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {

          //  var InkCanvas = new InkCanvas();

            InkStrokeContainer container = new InkStrokeContainer();
            //            container.AddStrokes(from item in strokes
            //select item.Clone());
            foreach (var item in _inkStrokes)
            {
                container.AddStrokes(from stroke in item.GetStrokes() select stroke.Clone());
            }
                //container.
                //IReadOnlyList<InkStroke> currentStrokes = InkCanvas.InkPresenter.StrokeContainer.GetStrokes();

                if (container.GetStrokes().Count >0) {
                Windows.Storage.Pickers.FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add("GIF with embedded ISF", new List<string>() { ".gif" });
                savePicker.DefaultFileExtension = ".gif";
                savePicker.SuggestedFileName = "PaperPencilPen001";

                Windows.Storage.StorageFile file =
                    await savePicker.PickSaveFileAsync();

                if (file != null)
                {
                    Windows.Storage.CachedFileManager.DeferUpdates(file);
                    IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);

                    using (IOutputStream outputStream = stream.GetOutputStreamAt(0))
                    {
                            await container.SaveAsync(outputStream);

                        await outputStream.FlushAsync();

                    }

                    stream.Dispose();

                    Windows.Storage.Provider.FileUpdateStatus status =
                        await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);

                    if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                    {
                        //file saved
                    }
                    else
                    {
                        //not saved
                    }
                }

                else {
                    //cancelled
                }

            }
        }

        private async void BtnOpen_Click(object sender, RoutedEventArgs e)
        {

            InkStrokeContainer container = new InkStrokeContainer();

            Windows.Storage.Pickers.FileOpenPicker openPicker = new Windows.Storage.Pickers.FileOpenPicker();
            openPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".gif");
            
            Windows.Storage.StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                IRandomAccessStream stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                using (var inputStream = stream.GetInputStreamAt(0))
                {
                    await container.LoadAsync(inputStream);
                }
                stream.Dispose();
                _inkStrokes.Clear();

                _inkStrokes.Add(container);
                DrawingCanvas.Invalidate();
            }


            else {

            }
        }

        private void EraseAllInk(object sender, RoutedEventArgs e)
        {
            //_pencilStrokes.Clear();
            //_otherStrokes.Clear();

            _inkStrokes.Clear();

            DrawingCanvas.Invalidate();
            _eraseAllFlyout.Hide();
        }

        private void Eraser_Unchecked(object sender, RoutedEventArgs e)
        {
         //   var unprocessedInput = InkCanvas.InkPresenter.UnprocessedInput;

            //unprocessedInput.PointerPressed -= UnprocessedInput_PointerPressed;
            //unprocessedInput.PointerMoved -= UnprocessedInput_PointerMoved;
            //unprocessedInput.PointerReleased -= UnprocessedInput_PointerReleased;
            //unprocessedInput.PointerExited -= UnprocessedInput_PointerExited;
            //unprocessedInput.PointerLost -= UnprocessedInput_PointerLost;

            InkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
        }

        private void Eraser_Checked(object sender, RoutedEventArgs e)
        {
            InkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            }

        private void UnprocessedInput_PointerLost(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (_isErasing) {
                args.Handled = true;
            }
            _isErasing = false;
        }

        private void UnprocessedInput_PointerExited(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (_isErasing)
            {
                args.Handled = true;
            }
            _isErasing = false;
        }

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (_isErasing)
            {
                args.Handled = true;
            }

            _isErasing = false;
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            if (!_isErasing | (bool) btnUndo.IsChecked)
            {
                return;
            }
            var invalidate = false;

            foreach (var item in _inkStrokes.ToArray())
            {
                if (item.GetStrokes().First().DrawingAttributes.Kind.ToString() != "Pencil")
                    continue;

                var rect = item.SelectWithLine(_lastpoint, args.CurrentPoint.Position);
                if (rect.IsEmpty) { continue; }
                if (rect.Width * rect.Height >0)
                {
                    int i = _inkStrokes.IndexOf(item);
                    _inkStrokes.Remove(item);

                    _erasedStrokes.Push(item);
                    _erasedIndices.Push(i);
                    history.Push(_erasedStrokes);
                    invalidate = true;
                }
                
            }
            _lastpoint = args.CurrentPoint.Position;
            args.Handled = true;
            if (invalidate)
            {
                DrawingCanvas.Invalidate();
            }
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, Windows.UI.Core.PointerEventArgs args)
        {
            _lastpoint = args.CurrentPoint.Position;
            args.Handled = true;
            _isErasing = true;
        }

        private void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            var strokes = _inkSynchronizer.BeginDry();
            var container = new InkStrokeContainer();

            container.AddStrokes(from item in strokes
                                 select item.Clone());

            _inkStrokes.Add(container);
            history.Push(_inkStrokes);

            _inkSynchronizer.EndDry();

            DrawingCanvas.Invalidate();
        }

        private void DrawCanvas(CanvasControl sender, CanvasDrawEventArgs args)
        {

            DrawInk(args.DrawingSession);
        
        }

        private void DrawInk(CanvasDrawingSession drawingSession)
        {

            foreach (var item in _inkStrokes) {
                var strokes = item.GetStrokes();
                drawingSession.DrawInk(strokes);
            }

       
       

        }

        private void InkToolbarPencilButton_Checked(object sender, RoutedEventArgs e)
        {
            _isPencil = true;
        }

        private void InkToolbarPencilButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _isPencil = false;
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (history.Count<1) return;

            var strokes = history.Pop() as IEnumerable<IInkStrokeContainer> ;
            if (strokes.Equals(_erasedStrokes))
            {
                var last = _erasedStrokes.Pop();
                var lastindex = _erasedIndices.Pop();
                _inkStrokes.Insert(lastindex, last);
            } else
            {

                _inkStrokes.RemoveAt(strokes.Count() - 1);

            }

            DrawingCanvas.Invalidate();
        }

    
    }
}
