using System.ComponentModel;
using Windows.ApplicationModel.DataTransfer;

namespace WinUI.Dock;

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed partial class DockTargetButton : UserControl
{
    public static readonly DependencyProperty DestinationProperty = DependencyProperty.Register(nameof(Destination),
                                                                                                typeof(Control),
                                                                                                typeof(DockTargetButton),
                                                                                                new PropertyMetadata(null));

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(nameof(Target),
                                                                                           typeof(DockTarget),
                                                                                           typeof(DockTargetButton),
                                                                                           new PropertyMetadata(DockTarget.Center));

    public DockTargetButton()
    {
        InitializeComponent();
    }

    public Control? Destination
    {
        get => (Control)GetValue(DestinationProperty);
        set => SetValue(DestinationProperty, value);
    }

    public DockTarget Target
    {
        get => (DockTarget)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    protected override async void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);

        // Capture UI-owned state before WinRT completes the data request on an arbitrary thread.
        var dispatcher_queue = DispatcherQueue;
        var destination = Destination;
        var target = Target;

        if (destination is DockManager manager)
        {
            var document_key = (string)await e.DataView.GetDataAsync(DragDropHelpers.DocumentKey);

            // Return preview changes to the XAML thread that owns the dock controls.
            if (DragDropHelpers.GetDocument(document_key) is Document document)
            {
                if (!dispatcher_queue.TryEnqueue(() => manager.ShowDockPreview(document, target)))
                {
                    System.Diagnostics.Debug.WriteLine("Dock preview was discarded because the UI dispatcher is shutting down.");
                }
            }
        }
        else if (destination is DocumentGroup group)
        {
            group.ShowDockPreview(target);
        }
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);

        if (Destination is DockManager manager)
        {
            manager.HideDockPreview();
        }
        else if (Destination is DocumentGroup group)
        {
            group.HideDockPreview();
        }
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);

        e.AcceptedOperation = DataPackageOperation.Move;
    }

    protected override async void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);

        // Capture dependency properties while still on the XAML thread.
        var dispatcher_queue = DispatcherQueue;
        var destination = Destination;
        var target = Target;
        var document_key = (string)await e.DataView.GetDataAsync(DragDropHelpers.DocumentKey);

        // Perform every dock-tree mutation on the dispatcher that owns the controls.
        if (DragDropHelpers.GetDocument(document_key) is Document document)
        {
            var drop_enqueued = dispatcher_queue.TryEnqueue(
                () =>
                {
                    if (destination is DockManager manager)
                    {
                        document.ResetPreferredSide(target);

                        manager.HideDockPreview();
                        manager.Dock(document, target);
                    }
                    else if (destination is DocumentGroup group)
                    {
                        group.HideDockPreview();
                        group.Dock(document, target);
                    }

                    document.Root!.HideDockTargets();
                }
            );
            if (!drop_enqueued)
                System.Diagnostics.Debug.WriteLine("Dock operation was discarded because the UI dispatcher is shutting down.");
        }
    }

    private void OnLoaded(object _, RoutedEventArgs __)
    {
        VisualStateManager.GoToState(this, Target.ToString(), false);
    }
}
