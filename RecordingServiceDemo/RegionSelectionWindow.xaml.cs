using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RecordingServiceDemo
{
    /// <summary>
    /// Region selection overlay window
    /// </summary>
    public partial class RegionSelectionWindow : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isSelecting;
        
        public Rectangle? SelectedRegion { get; private set; }
        public bool WasCancelled { get; private set; }

        public RegionSelectionWindow()
        {
            InitializeComponent();
            WasCancelled = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                WasCancelled = true;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(this);
            _isSelecting = true;
            
            SelectionRectangle.Visibility = Visibility.Visible;
            InfoBorder.Visibility = Visibility.Visible;
            
            Canvas.SetLeft(SelectionRectangle, _startPoint.X);
            Canvas.SetTop(SelectionRectangle, _startPoint.Y);
            SelectionRectangle.Width = 0;
            SelectionRectangle.Height = 0;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting)
                return;

            var currentPoint = e.GetPosition(this);
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            Canvas.SetLeft(SelectionRectangle, x);
            Canvas.SetTop(SelectionRectangle, y);
            SelectionRectangle.Width = width;
            SelectionRectangle.Height = height;
            
            // Update info text
            InfoText.Text = $"Position: ({(int)x}, {(int)y})  Size: {(int)width} × {(int)height}";
            Canvas.SetLeft(InfoBorder, x);
            Canvas.SetTop(InfoBorder, y - 30);
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting)
                return;

            _isSelecting = false;
            
            var currentPoint = e.GetPosition(this);
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var width = Math.Abs(currentPoint.X - _startPoint.X);
            var height = Math.Abs(currentPoint.Y - _startPoint.Y);
            
            // Only accept selection if it has some size
            if (width > 5 && height > 5)
            {
                // Get DPI scale factor
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0;
                double dpiY = 1.0;
                
                if (source != null)
                {
                    dpiX = source.CompositionTarget.TransformToDevice.M11;
                    dpiY = source.CompositionTarget.TransformToDevice.M22;
                }
                
                // Convert WPF window coordinates to screen coordinates
                var topLeft = this.PointToScreen(new System.Windows.Point(x, y));
                
                // Apply DPI scaling to dimensions
                var scaledWidth = (int)(width * dpiX);
                var scaledHeight = (int)(height * dpiY);
                
                SelectedRegion = new Rectangle(
                    (int)topLeft.X, 
                    (int)topLeft.Y, 
                    scaledWidth, 
                    scaledHeight);
                DialogResult = true;
            }
            else
            {
                WasCancelled = true;
            }
            
            Close();
        }
    }
}
