using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls.Generators;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using Avalonia.Styling;

namespace Avalonia.Controls
{
    /// <summary>
    /// Displays a collection of items.
    /// </summary>
    [PseudoClasses(":empty", ":singleitem")]
    public class ItemsControl : TemplatedControl, IChildIndexProvider, IScrollSnapPointsInfo
    {
        /// <summary>
        /// The default value for the <see cref="ItemsPanel"/> property.
        /// </summary>
        private static readonly FuncTemplate<Panel> DefaultPanel =
            new FuncTemplate<Panel>(() => new StackPanel());

        /// <summary>
        /// Defines the <see cref="Items"/> property.
        /// </summary>
        public static readonly DirectProperty<ItemsControl, IList?> ItemsProperty =
            AvaloniaProperty.RegisterDirect<ItemsControl, IList?>(nameof(Items), o => o.Items, (o, v) => o.Items = v);

        /// <summary>
        /// Defines the <see cref="ItemContainerTheme"/> property.
        /// </summary>
        public static readonly StyledProperty<ControlTheme?> ItemContainerThemeProperty =
            AvaloniaProperty.Register<ItemsControl, ControlTheme?>(nameof(ItemContainerTheme));

        /// <summary>
        /// Defines the <see cref="ItemCount"/> property.
        /// </summary>
        public static readonly DirectProperty<ItemsControl, int> ItemCountProperty =
            AvaloniaProperty.RegisterDirect<ItemsControl, int>(nameof(ItemCount), o => o.ItemCount);

        /// <summary>
        /// Defines the <see cref="ItemsPanel"/> property.
        /// </summary>
        public static readonly StyledProperty<ITemplate<Panel>> ItemsPanelProperty =
            AvaloniaProperty.Register<ItemsControl, ITemplate<Panel>>(nameof(ItemsPanel), DefaultPanel);

        /// <summary>
        /// Defines the <see cref="ItemsSource"/> property.
        /// </summary>
        public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
            AvaloniaProperty.Register<ItemsControl, IEnumerable?>(nameof(ItemsSource));

        /// <summary>
        /// Defines the <see cref="ItemTemplate"/> property.
        /// </summary>
        public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
            AvaloniaProperty.Register<ItemsControl, IDataTemplate?>(nameof(ItemTemplate));

        /// <summary>
        /// Defines the <see cref="ItemsView"/> property.
        /// </summary>
        public static readonly DirectProperty<ItemsControl, ItemsSourceView> ItemsViewProperty =
            AvaloniaProperty.RegisterDirect<ItemsControl, ItemsSourceView>(nameof(ItemsView), o => o.ItemsView);

        /// <summary>
        /// Defines the <see cref="DisplayMemberBinding" /> property
        /// </summary>
        public static readonly StyledProperty<IBinding?> DisplayMemberBindingProperty =
            AvaloniaProperty.Register<ItemsControl, IBinding?>(nameof(DisplayMemberBinding));

        /// <summary>
        /// Defines the <see cref="AreHorizontalSnapPointsRegular"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AreHorizontalSnapPointsRegularProperty =
            AvaloniaProperty.Register<ItemsControl, bool>(nameof(AreHorizontalSnapPointsRegular));

        /// <summary>
        /// Defines the <see cref="AreVerticalSnapPointsRegular"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AreVerticalSnapPointsRegularProperty =
            AvaloniaProperty.Register<ItemsControl, bool>(nameof(AreVerticalSnapPointsRegular));

        /// <summary>
        /// Gets or sets the <see cref="IBinding"/> to use for binding to the display member of each item.
        /// </summary>
        [AssignBinding]
        [InheritDataTypeFromItems(nameof(ItemsSource))]
        public IBinding? DisplayMemberBinding
        {
            get => GetValue(DisplayMemberBindingProperty);
            set => SetValue(DisplayMemberBindingProperty, value);
        }

        private IList? _items;
        private bool _itemsOverridden;
        private ItemsSourceView? _itemsView;
        private int _itemCount;
        private ItemContainerGenerator? _itemContainerGenerator;
        private EventHandler<ChildIndexChangedEventArgs>? _childIndexChanged;
        private IDataTemplate? _displayMemberItemTemplate;
        private ScrollViewer? _scrollViewer;
        private ItemsPresenter? _itemsPresenter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemsControl"/> class.
        /// </summary>
        public ItemsControl()
        {
            UpdatePseudoClasses(0);
        }

        /// <summary>
        /// Gets the <see cref="ItemContainerGenerator"/> for the control.
        /// </summary>
        public ItemContainerGenerator ItemContainerGenerator
        {
#pragma warning disable CS0612 // Type or member is obsolete
            get => _itemContainerGenerator ??= CreateItemContainerGenerator();
#pragma warning restore CS0612 // Type or member is obsolete
        }

        /// <summary>
        /// Gets or sets the items to display.
        /// </summary>
        [Content]
        public IList? Items
        {
            get
            {
                if (_items is null && !_itemsOverridden)
                    ItemsView = ItemsSourceView.GetOrCreate(_items = new ItemCollection(this));
                return _items;
            }

            [Obsolete("Use ItemsSource to set or bind items.")]
            set
            {
                if (_items != value || (value is null && !_itemsOverridden))
                {
                    if (_items is ItemCollection)
                    {
                        foreach (var item in _items)
                        {
                            if (item is ILogical logical)
                                LogicalChildren.Remove(logical);
                        }
                    }

                    var oldValue = _items;
                    _items = value;
                    _itemsOverridden = true;
                    ItemsView = ItemsSourceView.GetOrCreate(_items);
                    RaisePropertyChanged(ItemsProperty, oldValue, _items);
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="ControlTheme"/> that is applied to the container element generated for each item.
        /// </summary>
        public ControlTheme? ItemContainerTheme
        {
            get => GetValue(ItemContainerThemeProperty);
            set => SetValue(ItemContainerThemeProperty, value);
        }

        /// <summary>
        /// Gets the number of items being displayed by the <see cref="ItemsControl"/>.
        /// </summary>
        public int ItemCount
        {
            get => _itemCount;
            private set
            {
                if (SetAndRaise(ItemCountProperty, ref _itemCount, value))
                {
                    UpdatePseudoClasses(value);
                    _childIndexChanged?.Invoke(this, ChildIndexChangedEventArgs.TotalCountChanged);
                }
            }
        }

        /// <summary>
        /// Gets or sets the panel used to display the items.
        /// </summary>
        public ITemplate<Panel> ItemsPanel
        {
            get => GetValue(ItemsPanelProperty);
            set => SetValue(ItemsPanelProperty, value);
        }

        /// <summary>
        /// Gets or sets a collection used to generate the content of the <see cref="ItemsControl"/>.
        /// </summary>
        /// <remarks>
        /// Since Avalonia 11, <see cref="ItemsControl"/> has both an <see cref="Items"/> property
        /// and an <see cref="ItemsSource"/> property. The properties have the following differences:
        /// 
        /// <list type="bullet">
        /// <item><see cref="Items"/> is initialized with an empty collection and is a direct property,
        /// meaning that it cannot be styled </item>
        /// <item><see cref="ItemsSource"/> is by default null, and is a styled property. This property
        /// is marked as the content property and will be used for items added via inline XAML.</item>
        /// </list>
        /// 
        /// In Avalonia 11 the two properties can be used almost interchangeably but this will change
        /// in a later version. In order to be ready for this change, follow the following guidance:
        /// 
        /// <list type="bullet">
        /// <item>You should use the <see cref="Items"/> property when you're assigning a collection of
        /// item containers directly, for example adding a collection of <see cref="ListBoxItem"/>s
        /// directly to a <see cref="ListBox"/>.</item>
        /// <item>You should use the <see cref="ItemsSource"/> property when you're assigning or
        /// binding a collection of models which will be transformed by a data template.</item>
        /// </list>
        /// </remarks>
        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the data template used to display the items in the control.
        /// </summary>
        [InheritDataTypeFromItems(nameof(ItemsSource))]
        public IDataTemplate? ItemTemplate
        {
            get => GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        /// <summary>
        /// Gets the items presenter control.
        /// </summary>
        public ItemsPresenter? Presenter { get; private set; }

        /// <summary>
        /// Gets the <see cref="Panel"/> specified by <see cref="ItemsPanel"/>.
        /// </summary>
        public Panel? ItemsPanelRoot => Presenter?.Panel;

        /// <summary>
        /// Gets a standardized view over <see cref="Items"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="Items"/> property may be an enumerable which does not implement
        /// <see cref="IList"/> or may be null. This view can be used to provide a standardized
        /// view of the current items regardless of the type of the concrete collection, and
        /// without having to deal with null values.
        /// </remarks>
        public ItemsSourceView ItemsView
        {
            get
            {
                if (_itemsView is null)
                    ItemsView = ItemsSourceView.GetOrCreate(Items);
                return _itemsView!;
            }

            private set
            {
                if (ReferenceEquals(_itemsView, value))
                    return;

                if (_itemsView is not null)
                {
                    _itemsView.CollectionChanged -= ItemsCollectionChanged;

                    var oldValue = _itemsView;
                    _itemsView = value;
                    RaisePropertyChanged(ItemsViewProperty, oldValue, _itemsView);
                }
                else
                {
                    _itemsView = value;
                }

                ItemCount = _itemsView.Count;
                _itemsView.CollectionChanged += ItemsCollectionChanged;
            }
        }

        private protected bool WrapFocus { get; set; }

        event EventHandler<ChildIndexChangedEventArgs>? IChildIndexProvider.ChildIndexChanged
        {
            add => _childIndexChanged += value;
            remove => _childIndexChanged -= value;
        }

        /// <inheritdoc />
        public event EventHandler<RoutedEventArgs> HorizontalSnapPointsChanged
        {
            add
            {
                if (_itemsPresenter != null)
                {
                    _itemsPresenter.HorizontalSnapPointsChanged += value;
                }
            }

            remove
            {
                if (_itemsPresenter != null)
                {
                    _itemsPresenter.HorizontalSnapPointsChanged -= value;
                }
            }
        }

        /// <inheritdoc />
        public event EventHandler<RoutedEventArgs> VerticalSnapPointsChanged
        {
            add
            {
                if (_itemsPresenter != null)
                {
                    _itemsPresenter.VerticalSnapPointsChanged += value;
                }
            }

            remove
            {
                if (_itemsPresenter != null)
                {
                    _itemsPresenter.VerticalSnapPointsChanged -= value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the horizontal snap points for the <see cref="ItemsControl"/> are equidistant from each other.
        /// </summary>
        public bool AreHorizontalSnapPointsRegular
        {
            get => GetValue(AreHorizontalSnapPointsRegularProperty);
            set => SetValue(AreHorizontalSnapPointsRegularProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the vertical snap points for the <see cref="ItemsControl"/> are equidistant from each other.
        /// </summary>
        public bool AreVerticalSnapPointsRegular
        {
            get => GetValue(AreVerticalSnapPointsRegularProperty);
            set => SetValue(AreVerticalSnapPointsRegularProperty, value);
        }

        /// <summary>
        /// Returns the container for the item at the specified index.
        /// </summary>
        /// <param name="index">The index of the item to retrieve.</param>
        /// <returns>
        /// The container for the item at the specified index within the item collection, if the
        /// item has a container; otherwise, null.
        /// </returns>
        public Control? ContainerFromIndex(int index) => Presenter?.ContainerFromIndex(index);

        /// <summary>
        /// Returns the container corresponding to the specified item.
        /// </summary>
        /// <param name="item">The item to retrieve the container for.</param>
        /// <returns>
        /// A container that corresponds to the specified item, if the item has a container and
        /// exists in the collection; otherwise, null.
        /// </returns>
        public Control? ContainerFromItem(object item)
        {
            var index = ItemsView.IndexOf(item);
            return index >= 0 ? ContainerFromIndex(index) : null;
        }

        /// <summary>
        /// Returns the index to the item that has the specified, generated container.
        /// </summary>
        /// <param name="container">The generated container to retrieve the item index for.</param>
        /// <returns>
        /// The index to the item that corresponds to the specified generated container, or -1 if 
        /// <paramref name="container"/> is not found.
        /// </returns>
        public int IndexFromContainer(Control container) => Presenter?.IndexFromContainer(container) ?? -1;

        /// <summary>
        /// Returns the item that corresponds to the specified, generated container.
        /// </summary>
        /// <param name="container">The control that corresponds to the item to be returned.</param>
        /// <returns>
        /// The contained item, or the container if it does not contain an item.
        /// </returns>
        public object? ItemFromContainer(Control container)
        {
            var index = IndexFromContainer(container);
            return index >= 0 && index < ItemsView.Count ? ItemsView[index] : null;
        }

        /// <summary>
        /// Gets the currently realized containers.
        /// </summary>
        public IEnumerable<Control> GetRealizedContainers() => Presenter?.GetRealizedContainers() ?? Array.Empty<Control>();

        /// <summary>
        /// Creates or a container that can be used to display an item.
        /// </summary>
        protected internal virtual Control CreateContainerForItemOverride() => new ContentPresenter();

        /// <summary>
        /// Prepares the specified element to display the specified item.
        /// </summary>
        /// <param name="container">The element that's used to display the specified item.</param>
        /// <param name="item">The item to display.</param>
        /// <param name="index">The index of the item to display.</param>
        protected internal virtual void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            if (container == item)
                return;

            var itemTemplate = GetEffectiveItemTemplate();

            if (container is HeaderedContentControl hcc)
            {
                hcc.Content = item;

                if (item is IHeadered headered)
                    hcc.Header = headered.Header;
                else if (item is not Visual)
                    hcc.Header = item;

                if (itemTemplate is not null)
                    hcc.HeaderTemplate = itemTemplate;
            }
            else if (container is ContentControl cc)
            {
                cc.Content = item;
                if (itemTemplate is not null)
                    cc.ContentTemplate = itemTemplate;
            }
            else if (container is ContentPresenter p)
            {
                p.Content = item;
                if (itemTemplate is not null)
                    p.ContentTemplate = itemTemplate;
            }
            else if (container is ItemsControl ic)
            {
                if (itemTemplate is not null)
                    ic.ItemTemplate = itemTemplate;
                if (ItemContainerTheme is { } ict && !ict.IsSet(ItemContainerThemeProperty))
                    ic.ItemContainerTheme = ict;
            }

            // This condition is separate because HeaderedItemsControl needs to also run the
            // ItemsControl preparation.
            if (container is HeaderedItemsControl hic)
            {
                hic.Header = item;
                hic.HeaderTemplate = itemTemplate;

                itemTemplate ??= hic.FindDataTemplate(item) ?? this.FindDataTemplate(item);

                if (itemTemplate is ITreeDataTemplate treeTemplate)
                {
                    if (item is not null && treeTemplate.ItemsSelector(item) is { } itemsBinding)
                        BindingOperations.Apply(hic, ItemsSourceProperty, itemsBinding, null);
                }
            }
        }

        /// <summary>
        /// Called when the index for a container changes due to an insertion or removal in the
        /// items collection.
        /// </summary>
        /// <param name="container">The container whose index changed.</param>
        /// <param name="oldIndex">The old index.</param>
        /// <param name="newIndex">The new index.</param>
        protected virtual void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
        {
        }

        /// <summary>
        /// Undoes the effects of the <see cref="PrepareContainerForItemOverride(Control, object?, int)"/> method.
        /// </summary>
        /// <param name="container">The container element.</param>
        protected internal virtual void ClearContainerForItemOverride(Control container)
        {
            // Feels like we should be clearing the HeaderedItemsControl.Items binding here, but looking at
            // the WPF source it seems that this isn't done there.
        }

        /// <summary>
        /// Determines whether the specified item is (or is eligible to be) its own container.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>true if the item is (or is eligible to be) its own container; otherwise, false.</returns>
        protected internal virtual bool IsItemItsOwnContainerOverride(Control item) => true;

        /// <inheritdoc />
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
            _itemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
        }

        /// <summary>
        /// Handles directional navigation within the <see cref="ItemsControl"/>.
        /// </summary>
        /// <param name="e">The key events.</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!e.Handled)
            {
                var focus = FocusManager.Instance;
                var direction = e.Key.ToNavigationDirection();
                var container = Presenter?.Panel as INavigableContainer;

                if (container == null ||
                    focus?.Current == null ||
                    direction == null ||
                    direction.Value.IsTab())
                {
                    return;
                }

                Visual? current = focus.Current as Visual;

                while (current != null)
                {
                    if (current.VisualParent == container && current is IInputElement inputElement)
                    {
                        var next = GetNextControl(container, direction.Value, inputElement, WrapFocus);

                        if (next != null)
                        {
                            focus.Focus(next, NavigationMethod.Directional, e.KeyModifiers);
                            e.Handled = true;
                        }

                        break;
                    }

                    current = current.VisualParent;
                }
            }

            base.OnKeyDown(e);
        }

        /// <inheritdoc />
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ItemsControlAutomationPeer(this);
        }

        /// <inheritdoc />
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemContainerThemeProperty && _itemContainerGenerator is not null)
            {
                RefreshContainers();
            }
            else if (change.Property == ItemsSourceProperty)
            {
                if (Items is ItemCollection itemCollection)
                {
                    if (itemCollection.Count > 0)
                        throw new InvalidOperationException("Cannot set both Items and ItemsSource.");
                    else
                        itemCollection.IsReadOnly = change.GetNewValue<IEnumerable>() is not null;
                }
                else if (Items is not null)
                    throw new InvalidOperationException("Cannot set both Items and ItemsSource.");

                ItemsView = ItemsSourceView.GetOrCreate(change.GetNewValue<IEnumerable?>());
            }
            else if (change.Property == ItemTemplateProperty)
            {
                if (change.NewValue is not null && DisplayMemberBinding is not null)
                    throw new InvalidOperationException("Cannot set both DisplayMemberBinding and ItemTemplate.");
                RefreshContainers();
            }
            else if (change.Property == DisplayMemberBindingProperty)
            {
                if (change.NewValue is not null && ItemTemplate is not null)
                    throw new InvalidOperationException("Cannot set both DisplayMemberBinding and ItemTemplate.");
                _displayMemberItemTemplate = null;
                RefreshContainers();
            }
        }

        /// <summary>
        /// Refreshes the containers displayed by the control.
        /// </summary>
        /// <remarks>
        /// Causes all containers to be unrealized and re-realized.
        /// </remarks>
        protected void RefreshContainers() => Presenter?.Refresh();

        /// <summary>
        /// Called when the <see cref="INotifyCollectionChanged.CollectionChanged"/> event is
        /// raised on <see cref="Items"/>.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private protected virtual void ItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ItemCount = _itemsView?.Count ?? 0;
        }

        /// <summary>
        /// Creates the <see cref="ItemContainerGenerator"/>
        /// </summary>
        /// <remarks>
        /// This method is only present for backwards compatibility with 0.10.x in order for
        /// TreeView to be able to create a <see cref="TreeItemContainerGenerator"/>. Can be
        /// removed in 12.0.
        /// </remarks>
        [Obsolete]
        private protected virtual ItemContainerGenerator CreateItemContainerGenerator()
        {
            return new ItemContainerGenerator(this);
        }

        internal void AddLogicalChild(Control c)
        {
            if (!LogicalChildren.Contains(c))
                LogicalChildren.Add(c);
        }

        internal void RemoveLogicalChild(Control c) => LogicalChildren.Remove(c);

        /// <summary>
        /// Called by <see cref="ItemsPresenter"/> to register with the <see cref="ItemsControl"/>.
        /// </summary>
        /// <param name="presenter">The items presenter.</param>
        /// <remarks>
        /// ItemsPresenters can be within nested templates or in popups and so are not necessarily
        /// created immediately when the ItemsControl control's template is instantiated. Instead
        /// they register themselves using this method.
        /// </remarks>
        internal void RegisterItemsPresenter(ItemsPresenter presenter)
        {
            Presenter = presenter;
            _childIndexChanged?.Invoke(this, ChildIndexChangedEventArgs.ChildIndexesReset);
        }

        internal void PrepareItemContainer(Control container, object? item, int index)
        {
            var itemContainerTheme = ItemContainerTheme;

            if (itemContainerTheme is not null &&
                !container.IsSet(ThemeProperty) &&
                ((IStyleable)container).StyleKey == itemContainerTheme.TargetType)
            {
                container.Theme = itemContainerTheme;
            }

            if (item is not Control)
                container.DataContext = item;

            PrepareContainerForItemOverride(container, item, index);
        }

        internal void ItemContainerPrepared(Control container, object? item, int index)
        {
            _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(container, index));
            _scrollViewer?.RegisterAnchorCandidate(container);
        }

        internal void ItemContainerIndexChanged(Control container, int oldIndex, int newIndex)
        {
            ContainerIndexChangedOverride(container, oldIndex, newIndex);
            _childIndexChanged?.Invoke(this, new ChildIndexChangedEventArgs(container, newIndex));
        }

        internal void ClearItemContainer(Control container)
        {
            _scrollViewer?.UnregisterAnchorCandidate(container);
            ClearContainerForItemOverride(container);
        }

        private IDataTemplate? GetEffectiveItemTemplate()
        {
            if (ItemTemplate is { } itemTemplate)
                return itemTemplate;

            if (_displayMemberItemTemplate is null && DisplayMemberBinding is { } binding)
            {
                _displayMemberItemTemplate = new FuncDataTemplate<object?>((_, _) =>
                    new TextBlock
                    {
                        [!TextBlock.TextProperty] = binding,
                    });
            }

            return _displayMemberItemTemplate;
        }

        private void ItemsViewChanged()
        {

        }

        private void UpdatePseudoClasses(int itemCount)
        {
            PseudoClasses.Set(":empty", itemCount == 0);
            PseudoClasses.Set(":singleitem", itemCount == 1);
        }

        protected static IInputElement? GetNextControl(
            INavigableContainer container,
            NavigationDirection direction,
            IInputElement? from,
            bool wrap)
        {
            var current = from;

            for (;;)
            {
                var result = container.GetControl(direction, current, wrap);

                if (result is null)
                {
                    return null;
                }

                if (result.Focusable &&
                    result.IsEffectivelyEnabled &&
                    result.IsEffectivelyVisible)
                {
                    return result;
                }

                current = result;

                if (current == from)
                {
                    return null;
                }

                switch (direction)
                {
                    //We did not find an enabled first item. Move downwards until we find one.
                    case NavigationDirection.First:
                        direction = NavigationDirection.Down;
                        from = result;
                        break;

                    //We did not find an enabled last item. Move upwards until we find one.
                    case NavigationDirection.Last:
                        direction = NavigationDirection.Up;
                        from = result;
                        break;

                }
            }
        }

        int IChildIndexProvider.GetChildIndex(ILogical child)
        {
            return child is Control container ? IndexFromContainer(container) : -1;
        }

        bool IChildIndexProvider.TryGetTotalCount(out int count)
        {
            count = ItemsView.Count;
            return true;
        }

        /// <inheritdoc />
        public IReadOnlyList<double> GetIrregularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment)
        {
            return _itemsPresenter?.GetIrregularSnapPoints(orientation, snapPointsAlignment) ?? new List<double>();
        }

        /// <inheritdoc />
        public double GetRegularSnapPoints(Orientation orientation, SnapPointsAlignment snapPointsAlignment, out double offset)
        {
            offset = 0;

            return _itemsPresenter?.GetRegularSnapPoints(orientation, snapPointsAlignment, out offset) ?? 0;
        }

        private class ItemCollection : AvaloniaList<object>
        {
            private readonly ItemsControl _owner;

            public ItemCollection(ItemsControl owner)
            {
                _owner = owner;
                Validate = OnValidate;
                ResetBehavior = ResetBehavior.Remove;
                IsReadOnly = owner.ItemsSource is not null;
            }

            public override void Add(object item)
            {
                if (item is ILogical logical)
                    _owner.LogicalChildren.Add(logical);
                base.Add(item);
            }

            public override void AddRange(IEnumerable<object> items)
            {
                foreach (var item in items)
                {
                    if (item is ILogical logical)
                        _owner.LogicalChildren.Add(logical);
                }

                base.AddRange(items);
            }

            public override void Clear()
            {
                foreach (var item in this)
                {
                    if (item is ILogical logical)
                        _owner.LogicalChildren.Remove(logical);
                }

                base.Clear();
            }

            public override void Insert(int index, object item)
            {
                if (item is ILogical logical)
                    _owner.LogicalChildren.Add(logical);
                base.Insert(index, item);
            }

            public override void InsertRange(int index, IEnumerable<object> items)
            {
                foreach (var item in items)
                {
                    if (item is ILogical logical)
                        _owner.LogicalChildren.Add(logical);
                }

                base.InsertRange(index, items);
            }

            public override bool Remove(object item)
            {
                if (base.Remove(item))
                {
                    if (item is ILogical logical)
                        _owner.LogicalChildren.Remove(logical);
                    return true;
                }

                return false;
            }

            public override void RemoveAll(IEnumerable<object> items)
            {
                foreach (var item in items)
                {
                    if (item is ILogical logical)
                        _owner.LogicalChildren.Remove(logical);
                }

                base.RemoveAll(items);
            }

            public override void RemoveAt(int index)
            {
                if (this[index] is ILogical logical)
                    _owner.LogicalChildren.Remove(logical);
                base.RemoveAt(index);
            }

            public override void RemoveRange(int index, int count)
            {
                for (var i = index; i < index + count; ++i)
                {
                    if (this[i] is ILogical logical)
                        _owner.LogicalChildren.Remove(logical);
                }

                base.RemoveRange(index, count);
            }

            private void OnValidate(object obj)
            {
                if (IsReadOnly)
                    throw new InvalidOperationException("The ItemsControl is in ItemsSource mode.");
            }

            public bool IsReadOnly { get; set; }
        }
    }
}
