
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using SymbolLibrary.Contracts;
using SymbolLibrary.Core.Services;
using SymbolLibrary.Wpf;

using MessageBox = System.Windows.MessageBox;
using ListBox = System.Windows.Controls.ListBox;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Point = System.Windows.Point;

namespace SymbolLibrary.TestHost
{
    public partial class MainWindow : Window
    {
        private List<SymbolCategoryGroup> _allGroups = new();

        private DxfPreviewRenderer _previewRenderer = new();

        private DxfPreviewData _sampleDrawingData = null!;

        private DxfBoundingBox _sampleDrawingBounds = null!;

        private double _zoomScale = 1.0;

        // Matrix zoom allows zooming around the mouse position.
        private readonly MatrixTransform _viewTransform =
            new MatrixTransform();

        public MainWindow()
        {
            InitializeComponent();

            SymbolPreviewImage.RenderTransform = _viewTransform;

            LoadSymbolLibrary();

            LoadSampleDrawing();
        }

        // -------------------------------------------------------
        // 1. LOAD SYMBOL LIBRARY
        // -------------------------------------------------------

        private void LoadSymbolLibrary()
        {
            using (var dialog =
                new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description =
                    "Select the Sample Symbols Folder";

                dialog.UseDescriptionForTitle = true;

                dialog.ShowNewFolderButton = false;

                var result = dialog.ShowDialog();

                if (result != System.Windows.Forms.DialogResult.OK ||
                    string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    MessageBox.Show(
                        "Please select the symbol library folder.");

                    return;
                }

                try
                {
                    string rootFolder = dialog.SelectedPath;

                    SymbolLibraryService service =
                        new SymbolLibraryService();

                    var symbols = service.LoadSymbols(rootFolder);

                    var groups = symbols
                        .GroupBy(symbol =>
                            Path.GetDirectoryName(symbol.SymbolId)?
                                .Split(
                                    Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar)
                                .LastOrDefault() ?? "General")
                        .Select(group =>
                            new SymbolCategoryGroup(group.Key)
                            {
                                Symbols = group.ToList()
                            })
                        .ToList();

                    _allGroups = groups;

                    CategoryItemsControl.ItemsSource = _allGroups;

                    MessageBox.Show(
                        $"{symbols.Count} symbols loaded successfully.",
                        "Symbol Library",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to load symbols:\n" + ex.Message,
                        "Symbol Library Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // -------------------------------------------------------
        // 2. LOAD SAMPLE DRAWING
        // -------------------------------------------------------

        private void LoadSampleDrawing()
        {
            using (var dialog =
                new System.Windows.Forms.OpenFileDialog())
            {
                dialog.Title = "Select the Sample DXF Drawing";

                dialog.Filter =
                    "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*";

                dialog.Multiselect = false;

                dialog.CheckFileExists = true;

                dialog.CheckPathExists = true;

                var result = dialog.ShowDialog();

                if (result != System.Windows.Forms.DialogResult.OK ||
                    string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    MessageBox.Show(
                        "Please select a sample DXF drawing.",
                        "Sample Drawing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                try
                {
                    DxfSymbolReader reader = new DxfSymbolReader();

                    _sampleDrawingData =
                        reader.ReadPreviewData(dialog.FileName);

                    _sampleDrawingBounds =
                        reader.ReadBasicSymbolBounds(dialog.FileName);

                    _zoomScale = 1.0;

                    _viewTransform.Matrix = Matrix.Identity;

                    RenderDrawing();

                    SelectedSymbolName.Text = "Sample Floor Plan";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Unable to load sample drawing:\n\n" +
                        ex.Message,
                        "Drawing Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // -------------------------------------------------------
        // 3. RENDER DRAWING
        // -------------------------------------------------------

        private void RenderDrawing()
        {
            if (_sampleDrawingData == null)
                return;

            DrawingVisual visual = _previewRenderer.Render(
                _sampleDrawingData,
                800,
                500);

            SymbolPreviewImage.Source =
                new DrawingImage(visual.Drawing);
        }

        // -------------------------------------------------------
        // 4. SEARCH SYMBOLS
        // -------------------------------------------------------

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                CategoryItemsControl.ItemsSource = _allGroups;
                return;
            }

            var filteredGroups = _allGroups
                .Select(group =>
                    new SymbolCategoryGroup(group.Category)
                    {
                        Symbols = group.Symbols
                            .Where(symbol =>
                                symbol.Name.Contains(
                                    searchText,
                                    StringComparison.OrdinalIgnoreCase))
                            .ToList()
                    })
                .Where(group => group.Symbols.Count > 0)
                .ToList();

            CategoryItemsControl.ItemsSource = filteredGroups;
        }

        // -------------------------------------------------------
        // 5. SELECT SYMBOL
        // -------------------------------------------------------

        private void SymbolListBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox &&
                listBox.SelectedItem is SymbolPlacement selectedSymbol)
            {
                SelectedSymbolName.Text =
                    "Selected: " + selectedSymbol.Name;
            }
        }

        // -------------------------------------------------------
        // 6. DRAG SYMBOL FROM LIBRARY
        // -------------------------------------------------------

        private void SymbolListBox_PreviewMouseMove(
            object sender,
            System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton !=
                System.Windows.Input.MouseButtonState.Pressed)
            {
                return;
            }

            if (sender is ListBox listBox &&
                listBox.SelectedItem is SymbolPlacement selectedSymbol)
            {
                DataObject dragData = new DataObject(
                    SymbolDragFormat.Name,
                    selectedSymbol);

                DragDrop.DoDragDrop(
                    listBox,
                    dragData,
                    DragDropEffects.Copy);
            }
        }

        // -------------------------------------------------------
        // 7. DRAG OVER DRAWING
        // -------------------------------------------------------

        private void SymbolPreviewImage_DragOver(
            object sender,
            DragEventArgs e)
        {
            if (e.Data.GetDataPresent(SymbolDragFormat.Name))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        // -------------------------------------------------------
        // 8. DROP SYMBOL INTO DRAWING
        // -------------------------------------------------------

        private void SymbolPreviewImage_Drop(
            object sender,
            DragEventArgs e)
        {
            if (e.Data.GetData(SymbolDragFormat.Name)
                is not SymbolPlacement symbol)
            {
                return;
            }

            if (_sampleDrawingData == null)
                return;

            try
            {
                // Mouse position relative to the Image.
                // WPF accounts for the Image's RenderTransform
                // when returning this local position.
                Point imagePoint =
                    e.GetPosition(SymbolPreviewImage);

                // Image displays an 800 x 500 drawing.
                double fitScale = Math.Min(
                    SymbolPreviewImage.ActualWidth / 800.0,
                    SymbolPreviewImage.ActualHeight / 500.0);

                if (fitScale <= 0)
                    return;

                double offsetX =
                    (SymbolPreviewImage.ActualWidth -
                     800 * fitScale) / 2;

                double offsetY =
                    (SymbolPreviewImage.ActualHeight -
                     500 * fitScale) / 2;

                Point viewportPoint = new Point(
                    (imagePoint.X - offsetX) / fitScale,
                    (imagePoint.Y - offsetY) / fitScale);

                // Convert screen coordinates to CAD coordinates.
                Point cadPoint =
                    _previewRenderer.ScreenToDxf(viewportPoint);

                DxfSymbolReader reader =
                    new DxfSymbolReader();

                // Read geometry from the source DXF file.
                DxfPreviewData symbolData =
                    reader.ReadPreviewData(symbol.SourceFile);

                DxfBoundingBox symbolBounds =
                    reader.ReadBasicSymbolBounds(symbol.SourceFile);

                if (!symbolBounds.IsValid)
                {
                    MessageBox.Show(
                        "Unable to calculate symbol dimensions.",
                        "Placement Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                // Place the lower-left bounding-box corner
                // at the converted drop position.
                double moveX = cadPoint.X - symbolBounds.MinX;
                double moveY = cadPoint.Y - symbolBounds.MinY;

                AddSymbolGeometry(symbolData, moveX, moveY);

                // Re-render without resetting the zoom matrix.
                RenderDrawing();

                SelectedSymbolName.Text =
                    "Placed: " + symbol.Name;

                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to place symbol:\n\n" + ex.Message,
                    "Placement Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // -------------------------------------------------------
        // 9. ADD SYMBOL GEOMETRY
        // -------------------------------------------------------

        private void AddSymbolGeometry(
            DxfPreviewData symbolData,
            double moveX,
            double moveY)
        {
            // LINE
            foreach (var line in symbolData.Lines)
            {
                _sampleDrawingData.Lines.Add(new DxfLineData
                {
                    StartX = line.StartX + moveX,
                    StartY = line.StartY + moveY,
                    EndX = line.EndX + moveX,
                    EndY = line.EndY + moveY
                });
            }

            // CIRCLE
            foreach (var circle in symbolData.Circles)
            {
                _sampleDrawingData.Circles.Add(new DxfCircleData
                {
                    CenterX = circle.CenterX + moveX,
                    CenterY = circle.CenterY + moveY,
                    Radius = circle.Radius
                });
            }

            // ARC
            foreach (var arc in symbolData.Arcs)
            {
                _sampleDrawingData.Arcs.Add(new DxfArcData
                {
                    CenterX = arc.CenterX + moveX,
                    CenterY = arc.CenterY + moveY,
                    Radius = arc.Radius,
                    StartAngle = arc.StartAngle,
                    EndAngle = arc.EndAngle
                });
            }

            // POLYLINE
            foreach (var polyline in symbolData.Polylines)
            {
                DxfPolylineData newPolyline =
                    new DxfPolylineData
                    {
                        IsClosed = polyline.IsClosed
                    };

                foreach (var point in polyline.Points)
                {
                    newPolyline.Points.Add(
                        new DxfPolylinePoint
                        {
                            X = point.X + moveX,
                            Y = point.Y + moveY,
                            Bulge = point.Bulge
                        });
                }

                _sampleDrawingData.Polylines.Add(newPolyline);
            }

            // TEXT
            foreach (var text in symbolData.Texts)
            {
                _sampleDrawingData.Texts.Add(new DxfTextData
                {
                    Value = text.Value,
                    X = text.X + moveX,
                    Y = text.Y + moveY,
                    Height = text.Height,
                    Rotation = text.Rotation
                });
            }

            // MTEXT
            foreach (var text in symbolData.MTexts)
            {
                _sampleDrawingData.MTexts.Add(new DxfMTextData
                {
                    Value = text.Value,
                    X = text.X + moveX,
                    Y = text.Y + moveY,
                    Height = text.Height,
                    Rotation = text.Rotation
                });
            }

            // INSERT AND BLOCK CONTENT
            foreach (var insert in symbolData.Inserts)
            {
                _sampleDrawingData.Inserts.Add(
                    new DxfInsertData
                    {
                        BlockName = insert.BlockName,

                        X = insert.X + moveX,
                        Y = insert.Y + moveY,

                        BasePointX = insert.BasePointX,
                        BasePointY = insert.BasePointY,

                        ScaleX = insert.ScaleX,
                        ScaleY = insert.ScaleY,

                        Rotation = insert.Rotation,

                        BlockContent = insert.BlockContent
                    });
            }
        }

        // -------------------------------------------------------
        // 10. ZOOM AROUND MOUSE CURSOR
        // -------------------------------------------------------

        private void SymbolPreviewImage_PreviewMouseWheel(
            object sender,
            System.Windows.Input.MouseWheelEventArgs e)
        {
            // Get the mouse position relative to the Image.
            Point mousePosition =
                e.GetPosition(SymbolPreviewImage);

            // Calculate zoom factor.
            double zoomFactor = e.Delta > 0
                ? 1.1
                : 1.0 / 1.1;

            double newZoom = _zoomScale * zoomFactor;

            // Limit zoom.
            newZoom = Math.Max(0.5, Math.Min(5.0, newZoom));

            // Calculate the actual zoom factor after limits.
            zoomFactor = newZoom / _zoomScale;

            _zoomScale = newZoom;

            // Scale around the mouse cursor instead of the centre.
            Matrix matrix = _viewTransform.Matrix;

            matrix.ScaleAt(
                zoomFactor,
                zoomFactor,
                mousePosition.X,
                mousePosition.Y);

            _viewTransform.Matrix = matrix;

            e.Handled = true;
        }
    }
}
