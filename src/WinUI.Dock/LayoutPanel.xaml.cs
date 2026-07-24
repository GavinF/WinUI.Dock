using System.Text.Json.Nodes;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Media;

namespace WinUI.Dock;

[TemplatePart(Name = "PART_Root", Type = typeof(Grid))]
public partial class LayoutPanel : DockContainer
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation),
                                                                                                typeof(Orientation),
                                                                                                typeof(LayoutPanel),
                                                                                                new PropertyMetadata(Orientation.Vertical));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing),
                                                                                            typeof(double),
                                                                                            typeof(LayoutPanel),
                                                                                            new PropertyMetadata(12d, OnSplitterLayoutPropertyChanged));

    public static readonly DependencyProperty SplitterVisualThicknessProperty = DependencyProperty.Register(nameof(SplitterVisualThickness),
                                                                                                             typeof(double),
                                                                                                             typeof(LayoutPanel),
                                                                                                             new PropertyMetadata(12d, OnSplitterLayoutPropertyChanged));

    public static readonly DependencyProperty SplitterStyleProperty = DependencyProperty.Register(nameof(SplitterStyle),
                                                                                                   typeof(Style),
                                                                                                   typeof(LayoutPanel),
                                                                                                   new PropertyMetadata(null, OnSplitterLayoutPropertyChanged));

    private Grid? root;

    public LayoutPanel()
    {
        DefaultStyleKey = typeof(LayoutPanel);
    }

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double SplitterVisualThickness
    {
        get => (double)GetValue(SplitterVisualThicknessProperty);
        set => SetValue(SplitterVisualThicknessProperty, value);
    }

    public Style? SplitterStyle
    {
        get => (Style?)GetValue(SplitterStyleProperty);
        set => SetValue(SplitterStyleProperty, value);
    }

    protected override void InitTemplate()
    {
        root = GetTemplateChild("PART_Root") as Grid;
        ApplySplitterLayout();
    }

    protected override void InitChildren()
    {
        if (root is null)
        {
            return;
        }

        root.Children.Clear();

        foreach (DockContainer container in Children.Cast<DockContainer>())
        {
            root.Children.Add(container);
        }

        UpdateLayoutStructure();
    }

    protected override void SynchronizeChildren(DockModule[] oldChildren,
                                                int oldStartingIndex,
                                                DockModule[] newChildren,
                                                int newStartingIndex)
    {
        if (root is null)
        {
            return;
        }

        foreach (DockContainer container in oldChildren.Cast<DockContainer>())
        {
            root.Children.Remove(container);
        }

        foreach (DockContainer container in newChildren.Cast<DockContainer>())
        {
            root.Children.Add(container);
        }

        UpdateLayoutStructure();
    }

    protected override bool ValidateChildren()
    {
        return Children.All(static item => item is DockContainer);
    }

    protected override bool ConfirmEmptyContainer()
    {
        return true;
    }

    internal double CalculateHeight(DockModule module)
    {
        if (double.IsNaN(module.Height))
        {
            return Math.Clamp(ActualHeight / (Children.Count + 1), module.MinHeight, module.MaxHeight);
        }

        return Math.Clamp(module.Height, module.MinHeight, module.MaxHeight);
    }

    internal double CalculateWidth(DockModule module)
    {
        if (double.IsNaN(module.Width))
        {
            return Math.Clamp(ActualWidth / (Children.Count + 1), module.MinWidth, module.MaxWidth);
        }

        return Math.Clamp(module.Width, module.MinWidth, module.MaxWidth);
    }

    internal override void SaveLayout(JsonObject writer)
    {
        writer.WriteByModuleType(this);
        writer.WriteDockModuleProperties(this);
        writer.WriteDockContainerChildren(this);

        writer[nameof(Orientation)] = (int)Orientation;
    }

    internal override void LoadLayout(JsonObject reader)
    {
        reader.ReadDockModuleProperties(this);
        reader.ReadDockContainerChildren(this);

        Orientation = (Orientation)reader[nameof(Orientation)].Deserialize<int>();
    }

    private void UpdateLayoutStructure()
    {
        if (root is null)
        {
            return;
        }

        root.RowDefinitions.Clear();
        root.ColumnDefinitions.Clear();
        foreach (UIElement element in root.Children.Where(static item => item is GridSplitter))
        {
            root.Children.Remove(element);
        }

        if (Orientation is Orientation.Vertical)
        {
            foreach (DockModule module in Children)
            {
                bool isNaN = double.IsNaN(module.Height);

                RowDefinition row = new()
                {
                    MinHeight = module.MinHeight,
                    MaxHeight = module.MaxHeight,
                    Height = isNaN ? new(1, GridUnitType.Star) : new(module.Height, GridUnitType.Pixel)
                };

                if (!isNaN)
                {
                    row.RegisterPropertyChangedCallback(RowDefinition.HeightProperty, (_, _) => module.Height = row.Height.Value);
                }

                root.RowDefinitions.Add(row);

                Grid.SetRow(module, root.RowDefinitions.Count - 1);
            }

            for (int i = 1; i < Children.Count; i++)
            {
                GridSplitter splitter = CreateSplitter(GridSplitter.GridResizeDirection.Rows);

                Grid.SetRow(splitter, i);

                root.Children.Add(splitter);
            }
        }
        else
        {
            foreach (DockModule module in Children)
            {
                bool isNaN = double.IsNaN(module.Width);

                ColumnDefinition column = new()
                {
                    MinWidth = module.MinWidth,
                    MaxWidth = module.MaxWidth,
                    Width = isNaN ? new(1, GridUnitType.Star) : new(module.Width, GridUnitType.Pixel)
                };

                if (!isNaN)
                {
                    column.RegisterPropertyChangedCallback(ColumnDefinition.WidthProperty, (_, _) => module.Width = column.Width.Value);
                }

                root.ColumnDefinitions.Add(column);

                Grid.SetColumn(module, root.ColumnDefinitions.Count - 1);
            }

            for (int i = 1; i < Children.Count; i++)
            {
                GridSplitter splitter = CreateSplitter(GridSplitter.GridResizeDirection.Columns);

                Grid.SetColumn(splitter, i);

                root.Children.Add(splitter);
            }
        }
    }

    private static void OnSplitterLayoutPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is LayoutPanel panel)
        {
            panel.ApplySplitterLayout();
        }
    }

    private GridSplitter CreateSplitter(GridSplitter.GridResizeDirection resizeDirection)
    {
        GridSplitter splitter = new()
        {
            HorizontalAlignment = resizeDirection == GridSplitter.GridResizeDirection.Rows
                ? HorizontalAlignment.Stretch
                : HorizontalAlignment.Left,
            VerticalAlignment = resizeDirection == GridSplitter.GridResizeDirection.Rows
                ? VerticalAlignment.Top
                : VerticalAlignment.Stretch,
            ResizeDirection = resizeDirection,
            RenderTransform = new TranslateTransform()
        };

        splitter.Loaded += OnSplitterLoaded;
        splitter.SizeChanged += OnSplitterSizeChanged;
        ApplySplitterLayout(splitter);
        return splitter;
    }

    private void OnSplitterLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is GridSplitter splitter)
        {
            ApplySplitterTransform(splitter);
        }
    }

    private void OnSplitterSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (sender is GridSplitter splitter)
        {
            ApplySplitterTransform(splitter);
        }
    }

    private void ApplySplitterLayout()
    {
        if (root is null)
        {
            return;
        }

        double spacing = Math.Max(0, Spacing);
        root.ColumnSpacing = spacing;
        root.RowSpacing = spacing;

        foreach (GridSplitter splitter in root.Children.OfType<GridSplitter>())
        {
            ApplySplitterLayout(splitter);
        }
    }

    private void ApplySplitterLayout(GridSplitter splitter)
    {
        splitter.Resources["LayoutPanelSplitterVisualThickness"] = Math.Max(0, SplitterVisualThickness);
        splitter.Style = SplitterStyle ?? root?.Resources["PART_DefaultSplitterStyle"] as Style;
        ApplySplitterTransform(splitter);
    }

    private void ApplySplitterTransform(GridSplitter splitter)
    {
        bool resizeRows = splitter.ResizeDirection == GridSplitter.GridResizeDirection.Rows;
        double splitterExtent = resizeRows ? splitter.ActualHeight : splitter.ActualWidth;
        if (splitterExtent <= 0)
        {
            splitterExtent = resizeRows ? splitter.MinHeight : splitter.MinWidth;
        }
        if (splitterExtent <= 0)
        {
            splitterExtent = 12;
        }

        double centeredOffset = -(splitterExtent + Math.Max(0, Spacing)) / 2;
        if (splitter.RenderTransform is TranslateTransform transform)
        {
            transform.X = resizeRows ? 0 : centeredOffset;
            transform.Y = resizeRows ? centeredOffset : 0;
        }
    }
}
