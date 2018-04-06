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
        private readonly List<InkStrokeContainer> _pencilStrokes = new List<InkStrokeContainer>();
        private readonly List<InkStrokeContainer> _otherStrokes = new List<InkStrokeContainer>();


        public MainPage()
        {
            this.InitializeComponent();
        }

        private void EraserX_Click(object sender, RoutedEventArgs e)
        {

        }

       

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var inkPresenter = InkCanvas.InkPresenter;
            _inkSynchronizer = inkPresenter.ActivateCustomDrying();
            inkPresenter.StrokesCollected += InkPresenter_StrokesCollected;

            var eraser = InkToolbar.GetToolButton(InkToolbarTool.Eraser) as InkToolbarEraserButton;
            if (eraser !=null)
            {
                eraser.Checked += Eraser_Checked;
                eraser.Unchecked += Eraser_Unchecked;
            }

            var flyout = FlyoutBase.GetAttachedFlyout(eraser) as Flyout;

            if (flyout != null)
            {
                var button = flyout.Content as Button;
                if (button != null)
                {
                    var newButton = new Button();
                    newButton.Style = button.Style;
                    newButton.Content = button.Content;
                    newButton.Click += EraseAllInk;
                    flyout.Content = newButton;

                }

            }

     
        }

        private void EraseAllInk(object sender, RoutedEventArgs e)
        {
            _pencilStrokes.Clear();
            _otherStrokes.Clear();
            DrawingCanvas.Invalidate();
        }

        private void Eraser_Unchecked(object sender, RoutedEventArgs e)
        {
            var unprocessedInput = InkCanvas.InkPresenter.UnprocessedInput;

            unprocessedInput.PointerPressed -= UnprocessedInput_PointerPressed;
            unprocessedInput.PointerMoved -= UnprocessedInput_PointerMoved;
            unprocessedInput.PointerReleased -= UnprocessedInput_PointerReleased;
            unprocessedInput.PointerExited -= UnprocessedInput_PointerExited;
            unprocessedInput.PointerLost -= UnprocessedInput_PointerLost;

            InkCanvas.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
        }

        private void Eraser_Checked(object sender, RoutedEventArgs e)
        {
            var unprocessedInput = InkCanvas.InkPresenter.UnprocessedInput;
            unprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            unprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            unprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
            unprocessedInput.PointerExited += UnprocessedInput_PointerExited;
            unprocessedInput.PointerLost += UnprocessedInput_PointerLost;
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
            if (!_isErasing)
            {
                return;
            }
            var invalidate = false;

            foreach (var item in _pencilStrokes.ToArray())
            {
                var rect = item.SelectWithLine(_lastpoint, args.CurrentPoint.Position);
                if (rect.IsEmpty) { continue; }
                if (rect.Width * rect.Height >0)
                {
                    _pencilStrokes.Remove(item);
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

            if (_isPencil)
                _pencilStrokes.Add(container);
            else
                _otherStrokes.Add(container);

            _inkSynchronizer.EndDry();

            DrawingCanvas.Invalidate();
        }

        private void DrawCanvas(CanvasControl sender, CanvasDrawEventArgs args)
        {

            DrawInk(args.DrawingSession);
        
        }

        private void DrawInk(CanvasDrawingSession drawingSession)
        {

       
            foreach (var item in _pencilStrokes)
            {
                var strokes = item.GetStrokes();
               /* using (var list = new CanvasCommandList(drawingSession))
                {
                    using (var listSession = list.CreateDrawingSession())
                    {
                        listSession.DrawInk(strokes);
                    }
                }*/

                drawingSession.DrawInk(strokes);
            }
            foreach (var item in _otherStrokes)
            {
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
    }
}
