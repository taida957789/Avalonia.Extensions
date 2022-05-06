﻿using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.VisualTree;
using CSharpFunctionalExtensions;
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;

namespace Avalonia.Extensions.Media
{
    public class VideoView : NativeControlHost
    {
        public static readonly DirectProperty<VideoView, Maybe<MediaPlayer>> MediaPlayerProperty =
            AvaloniaProperty.RegisterDirect<VideoView, Maybe<MediaPlayer>>(nameof(MediaPlayer), o => o.MediaPlayer,
                (o, v) => o.MediaPlayer = v.GetValueOrDefault(), defaultBindingMode: BindingMode.TwoWay);
        private readonly BehaviorSubject<Maybe<MediaPlayer>> mediaPlayers = new BehaviorSubject<Maybe<MediaPlayer>>(Maybe<MediaPlayer>.None);
        private readonly BehaviorSubject<Maybe<IPlatformHandle>> platformHandles = new BehaviorSubject<Maybe<IPlatformHandle>>(Maybe<IPlatformHandle>.None);
        public static readonly StyledProperty<object> ContentProperty = ContentControl.ContentProperty.AddOwner<VideoView>();
        public static readonly StyledProperty<IBrush> BackgroundProperty = Panel.BackgroundProperty.AddOwner<VideoView>();
        private bool _isAttached;
        public IPlatformHandle Hndl;
        private Window _floatingContent;
        private IDisposable _disposables;
        private readonly IDisposable attacher;
        private IDisposable _isEffectivelyVisible;
        internal EventHandler Callback; 
        public VideoView()
        {
            attacher = platformHandles.WithLatestFrom(mediaPlayers).Subscribe(x =>
            {
                var playerAndHandle = from h in x.First from mp in x.Second select new { n = h, m = mp };
                playerAndHandle.Execute(a => a.m.SetHandle(a.n));
            });
            ContentProperty.Changed.AddClassHandler<VideoView>((s, e) => s.InitializeNativeOverlay());
            IsVisibleProperty.Changed.AddClassHandler<VideoView>((s, e) => s.ShowNativeOverlay(s.IsVisible));
        }
        public MediaPlayer MediaPlayer
        {
            get => mediaPlayers.Value.GetValueOrDefault();
            set => mediaPlayers.OnNext(value);
        }
        [Content]
        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }
        public IBrush Background
        {
            get => GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }
        public void SetContent(object o) => Content = o;
        private void InitializeNativeOverlay()
        {
            if (!((IVisual)this).IsAttachedToVisualTree)
                return;
            if (_floatingContent == null && Content != null)
            {
                _floatingContent = new Window()
                {
                    Opacity = 1,
                    CanResize = false,
                    ZIndex = 2147483647,
                    ShowInTaskbar = false,
                    Background = Brushes.Transparent,
                    SystemDecorations = SystemDecorations.None,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    TransparencyLevelHint = WindowTransparencyLevel.Transparent,
                };
                _floatingContent.PointerEnter += Controls_PointerEnter;
                _floatingContent.PointerLeave += Controls_PointerLeave;
                _disposables = new CompositeDisposable()
                {
                    _floatingContent.Bind(ContentControl.ContentProperty, this.GetObservable(ContentProperty)),
                    this.GetObservable(ContentProperty).Skip(1).Subscribe(_=> UpdateOverlayPosition()),
                    this.GetObservable(BoundsProperty).Skip(1).Subscribe(_ => UpdateOverlayPosition()),
                    Observable.FromEventPattern(VisualRoot, nameof(Window.PositionChanged))
                    .Subscribe(_ => UpdateOverlayPosition())
                };
            }
            ShowNativeOverlay(IsEffectivelyVisible);
        }
        public void Controls_PointerEnter(object sender, PointerEventArgs e)
        {
            Debug.WriteLine("POINTER ENTER");
            _floatingContent.Opacity = 0.8;
        }
        public void Controls_PointerLeave(object sender, PointerEventArgs e)
        {
            Debug.WriteLine("POINTER LEAVE");
            _floatingContent.Opacity = 0;
        }
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            try
            {
                var handle = base.CreateNativeControlCore(parent);
                platformHandles.OnNext(Maybe<IPlatformHandle>.From(handle));
                Hndl = handle;
                return handle;
            }
            finally
            {
                Callback?.Invoke(this, default);
            }
        }
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            attacher.Dispose();
            base.DestroyNativeControlCore(control);
            mediaPlayers.Value.Execute(MediaPlayerExtensions.DisposeHandle);
        }
        private void ShowNativeOverlay(bool show)
        {
            if (_floatingContent == null || _floatingContent.IsVisible == show)
                return;
            if (show && _isAttached)
                _floatingContent.Show(VisualRoot as Window);
            else
                _floatingContent.Hide();
        }
        private void UpdateOverlayPosition()
        {
            if (_floatingContent == null) return;
            bool forceSetWidth = false, forceSetHeight = false;
            var topLeft = new Point();
            var child = _floatingContent.Presenter?.Child;
            if (child?.IsArrangeValid == true)
            {
                switch (child.HorizontalAlignment)
                {
                    case Layout.HorizontalAlignment.Right:
                        topLeft = topLeft.WithX(Bounds.Width - _floatingContent.Bounds.Width);
                        break;
                    case Layout.HorizontalAlignment.Center:
                        topLeft = topLeft.WithX((Bounds.Width - _floatingContent.Bounds.Width) / 2);
                        break;
                    case Layout.HorizontalAlignment.Stretch:
                        forceSetWidth = true;
                        break;
                }
                switch (child.VerticalAlignment)
                {
                    case Layout.VerticalAlignment.Bottom:
                        topLeft = topLeft.WithY(Bounds.Height - _floatingContent.Bounds.Height);
                        break;
                    case Layout.VerticalAlignment.Center:
                        topLeft = topLeft.WithY((Bounds.Height - _floatingContent.Bounds.Height) / 2);
                        break;
                    case Layout.VerticalAlignment.Stretch:
                        forceSetHeight = true;
                        break;
                }
            }
            if (forceSetWidth && forceSetHeight)
                _floatingContent.SizeToContent = SizeToContent.Manual;
            else if (forceSetHeight)
                _floatingContent.SizeToContent = SizeToContent.Width;
            else if (forceSetWidth)
                _floatingContent.SizeToContent = SizeToContent.Height;
            else
                _floatingContent.SizeToContent = SizeToContent.Manual;
            _floatingContent.Width = forceSetWidth ? Bounds.Width : double.NaN;
            _floatingContent.Height = forceSetHeight ? Bounds.Height : double.NaN;
            _floatingContent.MaxWidth = Bounds.Width;
            _floatingContent.MaxHeight = Bounds.Height;
            var newPosition = this.PointToScreen(topLeft);
            if (newPosition != _floatingContent.Position)
                _floatingContent.Position = newPosition;
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttached = true;
            InitializeNativeOverlay();
            _isEffectivelyVisible = this.GetVisualAncestors().OfType<IControl>().Select(v => v.GetObservable(IsVisibleProperty))
                    .CombineLatest(v => !v.Any(o => !o)).DistinctUntilChanged().Subscribe(v => IsVisible = v);
        }
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _isEffectivelyVisible?.Dispose();
            ShowNativeOverlay(false);
            _isAttached = false;
        }
        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            _disposables?.Dispose();
            _disposables = null;
            _floatingContent?.Close();
            _floatingContent = null;
        }
    }
    public static class MediaPlayerExtensions
    {
        public static void DisposeHandle(this MediaPlayer player)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                player.Hwnd = IntPtr.Zero;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                player.XWindow = 0;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) player.NsObject = IntPtr.Zero;
        }
        public static void SetHandle(this MediaPlayer player, IPlatformHandle handle)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                player.Hwnd = handle.Handle;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                player.XWindow = (uint)handle.Handle;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) player.NsObject = handle.Handle;
        }
    }
}