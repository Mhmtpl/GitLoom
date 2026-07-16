using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using SkiaSharp.Views.Desktop;
using GitLoom.Rendering.Layout;

namespace GitLoom.Rendering.Controls;

public class CommitGraphControl : SKElement
{
    private static readonly SKColor[] LaneColors =
    [
        SKColor.Parse("#FF3B30"), // Vibrant Pink-Red
        SKColor.Parse("#007AFF"), // Vibrant Blue
        SKColor.Parse("#34C759"), // Vibrant Green
        SKColor.Parse("#FF9500"), // Vibrant Orange
        SKColor.Parse("#AF52DE"), // Vibrant Purple
        SKColor.Parse("#5AC8FA"), // Vibrant Light Blue
        SKColor.Parse("#FFCC00"), // Vibrant Yellow
        SKColor.Parse("#FF2D55")  // Vibrant Hot Pink
    ];

    private static readonly SKColor DarkBgColor = SKColor.Parse("#1E1E24");
    private static readonly SKColor SelectedRingColor = SKColor.Parse("#FFFFFF");

    private CommitNodeLayout? _dragStartNode;
    private Point _dragStartPoint;
    private Point _dragCurrentPoint;
    private bool _isDragging;

    // Dependency Properties
    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(CommitGraphLayout), typeof(CommitGraphControl),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty RowHeightProperty =
        DependencyProperty.Register(nameof(RowHeight), typeof(double), typeof(CommitGraphControl),
            new PropertyMetadata(40.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty ColumnWidthProperty =
        DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(CommitGraphControl),
            new PropertyMetadata(20.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty VerticalScrollOffsetProperty =
        DependencyProperty.Register(nameof(VerticalScrollOffset), typeof(double), typeof(CommitGraphControl),
            new PropertyMetadata(0.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty ViewportHeightProperty =
        DependencyProperty.Register(nameof(ViewportHeight), typeof(double), typeof(CommitGraphControl),
            new PropertyMetadata(0.0, OnVisualPropertyChanged));

    public static readonly DependencyProperty SelectedShaProperty =
        DependencyProperty.Register(nameof(SelectedSha), typeof(string), typeof(CommitGraphControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualPropertyChanged));

    public static readonly DependencyProperty HoveredShaProperty =
        DependencyProperty.Register(nameof(HoveredSha), typeof(string), typeof(CommitGraphControl),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty HeadShaProperty =
        DependencyProperty.Register(nameof(HeadSha), typeof(string), typeof(CommitGraphControl),
            new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty CommitsProperty =
        DependencyProperty.Register(nameof(Commits), typeof(System.Collections.IEnumerable), typeof(CommitGraphControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CheckoutBranchCommandProperty =
        DependencyProperty.Register(nameof(CheckoutBranchCommand), typeof(ICommand), typeof(CommitGraphControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MergeBranchCommandProperty =
        DependencyProperty.Register(nameof(MergeBranchCommand), typeof(ICommand), typeof(CommitGraphControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RebaseBranchCommandProperty =
        DependencyProperty.Register(nameof(RebaseBranchCommand), typeof(ICommand), typeof(CommitGraphControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CreateBranchCommandProperty =
        DependencyProperty.Register(nameof(CreateBranchCommand), typeof(ICommand), typeof(CommitGraphControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteBranchCommandProperty =
        DependencyProperty.Register(nameof(DeleteBranchCommand), typeof(ICommand), typeof(CommitGraphControl),
            new PropertyMetadata(null));

    public CommitGraphLayout? Layout
    {
        get => (CommitGraphLayout?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public System.Collections.IEnumerable? Commits
    {
        get => (System.Collections.IEnumerable?)GetValue(CommitsProperty);
        set => SetValue(CommitsProperty, value);
    }

    public ICommand? CheckoutBranchCommand
    {
        get => (ICommand?)GetValue(CheckoutBranchCommandProperty);
        set => SetValue(CheckoutBranchCommandProperty, value);
    }

    public ICommand? MergeBranchCommand
    {
        get => (ICommand?)GetValue(MergeBranchCommandProperty);
        set => SetValue(MergeBranchCommandProperty, value);
    }

    public ICommand? RebaseBranchCommand
    {
        get => (ICommand?)GetValue(RebaseBranchCommandProperty);
        set => SetValue(RebaseBranchCommandProperty, value);
    }

    public ICommand? CreateBranchCommand
    {
        get => (ICommand?)GetValue(CreateBranchCommandProperty);
        set => SetValue(CreateBranchCommandProperty, value);
    }

    public ICommand? DeleteBranchCommand
    {
        get => (ICommand?)GetValue(DeleteBranchCommandProperty);
        set => SetValue(DeleteBranchCommandProperty, value);
    }

    public double RowHeight
    {
        get => (double)GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public double ColumnWidth
    {
        get => (double)GetValue(ColumnWidthProperty);
        set => SetValue(ColumnWidthProperty, value);
    }

    public double VerticalScrollOffset
    {
        get => (double)GetValue(VerticalScrollOffsetProperty);
        set => SetValue(VerticalScrollOffsetProperty, value);
    }

    public double ViewportHeight
    {
        get => (double)GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    public string? SelectedSha
    {
        get => (string?)GetValue(SelectedShaProperty);
        set => SetValue(SelectedShaProperty, value);
    }

    public string? HoveredSha
    {
        get => (string?)GetValue(HoveredShaProperty);
        set => SetValue(HoveredShaProperty, value);
    }

    public string? HeadSha
    {
        get => (string?)GetValue(HeadShaProperty);
        set => SetValue(HeadShaProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CommitGraphControl control)
        {
            if (e.Property == LayoutProperty || 
                e.Property == ColumnWidthProperty || 
                e.Property == RowHeightProperty)
            {
                control.InvalidateMeasure();
            }
            control.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Layout != null)
        {
            double desiredWidth = (Layout.MaxLanes + 1) * ColumnWidth;
            double desiredHeight = double.IsInfinity(availableSize.Height) ? (Layout.Nodes.Count * RowHeight) : availableSize.Height;
            return new Size(desiredWidth, desiredHeight);
        }
        return base.MeasureOverride(availableSize);
    }

    public CommitGraphControl()
    {
        ClipToBounds = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Layout == null || Layout.Nodes.Count == 0)
        {
            return;
        }

        int totalRows = Layout.Nodes.Count;
        double rHeight = RowHeight;
        double cWidth = ColumnWidth;
        double scrollY = VerticalScrollOffset;
        double viewH = ViewportHeight > 0 ? ViewportHeight : ActualHeight;

        // Calculate visible range
        int startRow = Math.Max(0, (int)(scrollY / rHeight) - 1);
        int endRow = Math.Min(totalRows - 1, (int)((scrollY + viewH) / rHeight) + 1);

        // Get DPI scale factor
        var dpi = VisualTreeHelper.GetDpi(this);
        float scaleX = (float)dpi.DpiScaleX;
        float scaleY = (float)dpi.DpiScaleY;

        canvas.Save();
        canvas.Scale(scaleX, scaleY);
        canvas.Translate(0, -(float)scrollY);

        // Draw Edges First (so nodes sit on top)
        using var linePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.8f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        foreach (var edge in Layout.Edges)
        {
            // Only draw edges that intersect the visible viewport
            int minRow = Math.Min(edge.SourceRow, edge.TargetRow);
            int maxRow = Math.Max(edge.SourceRow, edge.TargetRow);
            if (maxRow < startRow || minRow > endRow) continue;

            linePaint.Color = GetLaneColor(edge.ColorIndex);

            float xStart = (float)((edge.SourceLane + 0.5) * cWidth);
            float yStart = (float)((edge.SourceRow + 0.5) * rHeight);
            float xEnd = (float)((edge.TargetLane + 0.5) * cWidth);
            float yEnd = (float)((edge.TargetRow + 0.5) * rHeight);

            if (edge.Type == EdgeType.Straight)
            {
                canvas.DrawLine(xStart, yStart, xEnd, yEnd, linePaint);
            }
            else if (edge.Type == EdgeType.BranchSplit)
            {
                // Line goes vertically down from child until TargetRow - 1, then curves to parent
                float yCurveStart = (float)((edge.TargetRow - 0.5) * rHeight);
                if (edge.SourceRow < edge.TargetRow - 1)
                {
                    canvas.DrawLine(xStart, yStart, xStart, yCurveStart, linePaint);
                }

                using var path = new SKPath();
                path.MoveTo(xStart, yCurveStart);
                float ctrlY1 = yCurveStart + (float)(rHeight * 0.5);
                float ctrlY2 = yEnd - (float)(rHeight * 0.5);
                path.CubicTo(xStart, ctrlY1, xEnd, ctrlY2, xEnd, yEnd);
                canvas.DrawPath(path, linePaint);
            }
            else if (edge.Type == EdgeType.Merge)
            {
                // Line curves immediately from child to target lane at row + 1, then goes straight down
                float yCurveEnd = (float)((edge.SourceRow + 1.5) * rHeight);
                
                using var path = new SKPath();
                path.MoveTo(xStart, yStart);
                float ctrlY1 = yStart + (float)(rHeight * 0.5);
                float ctrlY2 = yCurveEnd - (float)(rHeight * 0.5);
                path.CubicTo(xStart, ctrlY1, xEnd, ctrlY2, xEnd, yCurveEnd);
                canvas.DrawPath(path, linePaint);

                if (edge.SourceRow + 1 < edge.TargetRow)
                {
                    canvas.DrawLine(xEnd, yCurveEnd, xEnd, yEnd, linePaint);
                }
            }
        }

        // Draw Nodes
        using var nodeFillPaint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
        using var nodeBorderPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColor.Parse("#1D212A"), // Matches AppBackground for perfect halo
            StrokeWidth = 2.0f,
            IsAntialias = true
        };
        using var glowPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw highlights/glow for hovered commit node
        if (!string.IsNullOrEmpty(HoveredSha) && Layout.Nodes.TryGetValue(HoveredSha, out var hoveredNode))
        {
            if (hoveredNode.Row >= startRow && hoveredNode.Row <= endRow)
            {
                float x = (float)((hoveredNode.Lane + 0.5) * cWidth);
                float y = (float)((hoveredNode.Row + 0.5) * rHeight);
                glowPaint.Color = GetLaneColor(hoveredNode.Lane).WithAlpha(60);
                canvas.DrawCircle(x, y, 12f, glowPaint);
            }
        }

        // Draw all visible nodes
        for (int r = startRow; r <= endRow; r++)
        {
            // Find commit node layout at this row
            CommitNodeLayout? node = null;
            foreach (var n in Layout.Nodes.Values)
            {
                if (n.Row == r)
                {
                    node = n;
                    break;
                }
            }

            if (node == null) continue;

            float x = (float)((node.Lane + 0.5) * cWidth);
            float y = (float)((node.Row + 0.5) * rHeight);

            bool isSelected = node.Sha == SelectedSha;
            bool isHead = node.Sha == HeadSha;
            bool isHovered = node.Sha == HoveredSha;

            float radius = 5.5f;
            if (isHovered) radius = 7.0f;
            else if (isSelected) radius = 6.5f;

            if (node.Sha == "WIP")
            {
                using var wipBorderPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = GetLaneColor(node.Lane),
                    StrokeWidth = 2.2f,
                    PathEffect = SKPathEffect.CreateDash([3.0f, 2.5f], 0.0f),
                    IsAntialias = true
                };
                nodeFillPaint.Color = SKColor.Parse("#1D212A");
                canvas.DrawCircle(x, y, radius, nodeFillPaint);
                canvas.DrawCircle(x, y, radius, wipBorderPaint);
            }
            else
            {
                nodeFillPaint.Color = GetLaneColor(node.Lane);
                canvas.DrawCircle(x, y, radius, nodeFillPaint);
                canvas.DrawCircle(x, y, radius, nodeBorderPaint);
            }

            // Draw HEAD indicator (inner dot or outer ring)
            if (isHead)
            {
                nodeFillPaint.Color = SKColors.White;
                canvas.DrawCircle(x, y, radius * 0.4f, nodeFillPaint);
            }

            // Draw selected double ring
            if (isSelected)
            {
                using var selectRingPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    Color = SelectedRingColor,
                    StrokeWidth = 1.5f,
                    IsAntialias = true
                };
                canvas.DrawCircle(x, y, radius + 3f, selectRingPaint);
            }
        }

        // Draw drag connection line if dragging
        if (_isDragging && _dragStartNode != null)
        {
            float startX = (float)((_dragStartNode.Lane + 0.5) * cWidth);
            float startY = (float)((_dragStartNode.Row + 0.5) * rHeight);
            float endX = (float)_dragCurrentPoint.X;
            float endY = (float)(_dragCurrentPoint.Y + scrollY);

            // Draw line from source node to current mouse pointer
            using var dragPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = GetLaneColor(_dragStartNode.Lane),
                StrokeWidth = 3.0f,
                PathEffect = SKPathEffect.CreateDash([4.0f, 3.0f], 0.0f),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawLine(startX, startY, endX, endY, dragPaint);

            // Draw a subtle animated pulsing circle under the cursor during drag
            using var pulsePaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = GetLaneColor(_dragStartNode.Lane).WithAlpha(120),
                IsAntialias = true
            };
            canvas.DrawCircle(endX, endY, 6.0f, pulsePaint);

            // Draw a floating pill badge label at the end point
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true
            };
            
            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10f);
            
            var text = "Drag to Merge/Rebase";
            var textWidth = font.MeasureText(text);
            var rectWidth = textWidth + 16f;
            var rectHeight = 18f;
            var rect = new SKRect(endX - rectWidth/2, endY - 25f - rectHeight, endX + rectWidth/2, endY - 25f);
            
            using var badgePaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColor.Parse("#1E1E24"),
                IsAntialias = true
            };
            using var badgeBorderPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = GetLaneColor(_dragStartNode.Lane),
                StrokeWidth = 1.0f,
                IsAntialias = true
            };
            canvas.DrawRoundRect(rect, 4f, 4f, badgePaint);
            canvas.DrawRoundRect(rect, 4f, 4f, badgeBorderPaint);
            canvas.DrawText(text, endX, endY - 25f - 5f, SKTextAlign.Center, font, textPaint);
        }

        canvas.Restore();
    }

    private SKColor GetLaneColor(int index)
    {
        if (index < 0) return SKColors.Gray;
        return LaneColors[index % LaneColors.Length];
    }

    // Interactivity
    private CommitNodeLayout? HitTestNode(Point mousePos)
    {
        if (Layout == null) return null;
        double canvasY = mousePos.Y + VerticalScrollOffset;
        int row = (int)(canvasY / RowHeight);
        if (row < 0 || row >= Layout.Nodes.Count) return null;

        foreach (var n in Layout.Nodes.Values)
        {
            if (n.Row == row)
            {
                double nodeX = (n.Lane + 0.5) * ColumnWidth;
                double nodeY = (n.Row + 0.5) * RowHeight;
                double dx = mousePos.X - nodeX;
                double dy = canvasY - nodeY;
                if (Math.Sqrt(dx * dx + dy * dy) <= 15.0)
                {
                    return n;
                }
            }
        }
        return null;
    }

    private dynamic? FindCommitViewModel(string sha)
    {
        if (Commits == null) return null;
        foreach (var item in Commits)
        {
            try
            {
                dynamic vm = item;
                if (vm.Sha == sha)
                {
                    return vm;
                }
            }
            catch { }
        }
        return null;
    }

    private List<string> GetBranches(string sha)
    {
        var branches = new List<string>();
        var vm = FindCommitViewModel(sha);
        if (vm != null)
        {
            try
            {
                foreach (var b in vm.Branches)
                {
                    branches.Add(b.ToString());
                }
            }
            catch { }
        }
        return branches;
    }

    private void ShowRightClickMenu(CommitNodeLayout node, Point clientPos)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E24"));
        menu.Foreground = System.Windows.Media.Brushes.White;
        menu.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E4452"));

        var sha = node.Sha;
        var branches = GetBranches(sha);

        if (branches.Count > 0)
        {
            foreach (var b in branches)
            {
                var checkoutItem = new System.Windows.Controls.MenuItem { Header = $"Checkout '{b}'", Foreground = System.Windows.Media.Brushes.White };
                checkoutItem.Click += (s, e) => CheckoutBranchCommand?.Execute(b);
                menu.Items.Add(checkoutItem);

                var mergeItem = new System.Windows.Controls.MenuItem { Header = $"Merge '{b}' into current", Foreground = System.Windows.Media.Brushes.White };
                mergeItem.Click += (s, e) => MergeBranchCommand?.Execute(b);
                menu.Items.Add(mergeItem);

                var rebaseItem = new System.Windows.Controls.MenuItem { Header = $"Rebase current onto '{b}'", Foreground = System.Windows.Media.Brushes.White };
                rebaseItem.Click += (s, e) => RebaseBranchCommand?.Execute(Tuple.Create(b, ""));
                menu.Items.Add(rebaseItem);

                var deleteItem = new System.Windows.Controls.MenuItem { Header = $"Delete branch '{b}'", Foreground = System.Windows.Media.Brushes.White };
                deleteItem.Click += (s, e) => DeleteBranchCommand?.Execute(b);
                menu.Items.Add(deleteItem);
            }
            menu.Items.Add(new System.Windows.Controls.Separator { Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E4452")) });
        }

        var createBranchItem = new System.Windows.Controls.MenuItem { Header = "Create branch here...", Foreground = System.Windows.Media.Brushes.White };
        createBranchItem.Click += (s, e) => CreateBranchCommand?.Execute(sha);
        menu.Items.Add(createBranchItem);

        menu.IsOpen = true;
    }

    private void ShowDragDropMenu(CommitNodeLayout sourceNode, CommitNodeLayout targetNode, Point clientPos)
    {
        var sourceBranches = GetBranches(sourceNode.Sha);
        var targetBranches = GetBranches(targetNode.Sha);

        if (sourceBranches.Count == 0) return;

        var menu = new System.Windows.Controls.ContextMenu();
        menu.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E24"));
        menu.Foreground = System.Windows.Media.Brushes.White;
        menu.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E4452"));

        foreach (var s in sourceBranches)
        {
            if (targetBranches.Count > 0)
            {
                foreach (var t in targetBranches)
                {
                    var mergeItem = new System.Windows.Controls.MenuItem { Header = $"Merge '{s}' into '{t}'", Foreground = System.Windows.Media.Brushes.White };
                    mergeItem.Click += (sender, e) => {
                        CheckoutBranchCommand?.Execute(t);
                        MergeBranchCommand?.Execute(s);
                    };
                    menu.Items.Add(mergeItem);

                    var rebaseItem = new System.Windows.Controls.MenuItem { Header = $"Rebase '{s}' onto '{t}'", Foreground = System.Windows.Media.Brushes.White };
                    rebaseItem.Click += (sender, e) => {
                        RebaseBranchCommand?.Execute(Tuple.Create(t, s));
                    };
                    menu.Items.Add(rebaseItem);
                }
            }
            else
            {
                var targetSha = targetNode.Sha.Substring(0, 7);
                var rebaseCommitItem = new System.Windows.Controls.MenuItem { Header = $"Rebase '{s}' onto '{targetSha}'", Foreground = System.Windows.Media.Brushes.White };
                rebaseCommitItem.Click += (sender, e) => {
                    RebaseBranchCommand?.Execute(Tuple.Create(targetNode.Sha, s));
                };
                menu.Items.Add(rebaseCommitItem);
            }
        }

        if (menu.Items.Count > 0)
        {
            menu.IsOpen = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Layout == null || Layout.Nodes.Count == 0) return;

        var pos = e.GetPosition(this);

        if (IsMouseCaptured && _dragStartNode != null)
        {
            if (!_isDragging)
            {
                double dx = pos.X - _dragStartPoint.X;
                double dy = pos.Y - _dragStartPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > 5.0)
                {
                    _isDragging = true;
                }
            }

            if (_isDragging)
            {
                _dragCurrentPoint = pos;
                InvalidateVisual();
            }
            return;
        }

        var node = HitTestNode(pos);
        if (node != null)
        {
            if (HoveredSha != node.Sha)
            {
                HoveredSha = node.Sha;
                Cursor = Cursors.Hand;
            }
        }
        else
        {
            if (HoveredSha != null)
            {
                HoveredSha = null;
                Cursor = Cursors.Arrow;
            }
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HoveredSha = null;
        if (!_isDragging)
        {
            _dragStartNode = null;
            IsMouseCaptured.ToString(); // Dummy to read
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (Layout == null || Layout.Nodes.Count == 0) return;

        var pos = e.GetPosition(this);
        var clickedNode = HitTestNode(pos);

        if (e.ChangedButton == MouseButton.Left)
        {
            if (clickedNode != null)
            {
                _dragStartNode = clickedNode;
                _dragStartPoint = pos;
                _dragCurrentPoint = pos;
                _isDragging = false;
                CaptureMouse();
                e.Handled = true;
            }
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            if (clickedNode != null)
            {
                ShowRightClickMenu(clickedNode, PointToScreen(pos));
                e.Handled = true;
            }
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            var pos = e.GetPosition(this);

            if (_isDragging && _dragStartNode != null)
            {
                _isDragging = false;
                var dropNode = HitTestNode(pos);
                if (dropNode != null && dropNode.Sha != _dragStartNode.Sha)
                {
                    ShowDragDropMenu(_dragStartNode, dropNode, PointToScreen(pos));
                }
                InvalidateVisual();
            }
            else if (_dragStartNode != null)
            {
                // Simple click selection
                SelectedSha = _dragStartNode.Sha;
            }

            _dragStartNode = null;
            e.Handled = true;
        }
    }
}
