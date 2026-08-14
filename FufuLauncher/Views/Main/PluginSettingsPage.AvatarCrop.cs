/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;
using FufuLauncher.Helpers;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private Windows.Foundation.Point _cropPointerPosition;
    private bool _isCropDragging = false;
    private uint _originalImageWidth;
    private uint _originalImageHeight;
    private string _editingImagePath;

    private void CropScrollViewer_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(CropScrollViewer);
        if (point.Properties.IsLeftButtonPressed)
        {
            _isCropDragging = true;
            _cropPointerPosition = point.Position;
            CropScrollViewer.CapturePointer(e.Pointer);
        }
    }

    private void CropScrollViewer_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isCropDragging)
        {
            var point = e.GetCurrentPoint(CropScrollViewer);
            var deltaX = point.Position.X - _cropPointerPosition.X;
            var deltaY = point.Position.Y - _cropPointerPosition.Y;
        
            CropScrollViewer.ChangeView(
                CropScrollViewer.HorizontalOffset - deltaX,
                CropScrollViewer.VerticalOffset - deltaY,
                null);
            
            _cropPointerPosition = point.Position;
        }
    }

    private void CropScrollViewer_PointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_isCropDragging)
        {
            _isCropDragging = false;
            CropScrollViewer.ReleasePointerCapture(e.Pointer);
        }
    }

    private void CropScrollViewer_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(CropScrollViewer).Properties.MouseWheelDelta;
        if (delta == 0) return;

        double newZoom = CropScrollViewer.ZoomFactor + (delta > 0 ? 0.2 : -0.2);
        newZoom = Math.Max(CropScrollViewer.MinZoomFactor, Math.Min(newZoom, CropScrollViewer.MaxZoomFactor));
    
        CropScrollViewer.ChangeView(null, null, (float)newZoom);
        e.Handled = true;
    }

    private async Task SaveCroppedImageAsync(int targetSize)
    {
        double viewSize = 300.0;
        double baseScale = Math.Max(viewSize / _originalImageWidth, viewSize / _originalImageHeight);
        double finalScale = baseScale * CropScrollViewer.ZoomFactor;

        double cropX = CropScrollViewer.HorizontalOffset / finalScale;
        double cropY = CropScrollViewer.VerticalOffset / finalScale;
        double cropSize = viewSize / finalScale;
        
        int x = Math.Max(0, (int)Math.Floor(cropX));
        int y = Math.Max(0, (int)Math.Floor(cropY));
        int size = (int)Math.Ceiling(cropSize);
        
        using (var image = await SixLabors.ImageSharp.Image.LoadAsync(_editingImagePath))
        {
            int safeX = Math.Min(x, image.Width - 1);
            int safeY = Math.Min(y, image.Height - 1);
            int safeWidth = Math.Min(size, image.Width - safeX);
            int safeHeight = Math.Min(size, image.Height - safeY);
            int finalCropSize = Math.Max(1, Math.Min(safeWidth, safeHeight));

            image.Mutate(ctx => ctx
                .Crop(new Rectangle(safeX, safeY, finalCropSize, finalCropSize))
                .Resize(targetSize, targetSize, KnownResamplers.Bicubic));

            var outputPath = ViewModel.GetAvatarPath(targetSize);
            var directory = Path.GetDirectoryName(outputPath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await image.SaveAsPngAsync(outputPath);
        }
    }

    private async Task LoadImageToCropperAsync(string filePath)
    {
        try
        {
            _editingImagePath = filePath;
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                _originalImageWidth = decoder.OrientedPixelWidth;
                _originalImageHeight = decoder.OrientedPixelHeight;
            }

            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            bmp.CreateOptions = Microsoft.UI.Xaml.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(filePath);
        
            CropTargetImage.Source = bmp;

            double viewSize = 300.0;
            double scale = Math.Max(viewSize / _originalImageWidth, viewSize / _originalImageHeight);
        
            CropTargetImage.Width = _originalImageWidth * scale;
            CropTargetImage.Height = _originalImageHeight * scale;
        
            await Task.Delay(50);
            CropScrollViewer.ChangeView(0, 0, 1.0f, true);
        
            await CropImageDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_LoadFail_Title".GetLocalized(), ex.Message, NotificationType.Error));
        }
    }

    private async void OnCropSaveClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await SaveCroppedImageAsync(_currentEditSize);
            ViewModel.UpdateAvatarPreview();
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Success".GetLocalized(), "Avatar_Crop_Success".GetLocalized(), NotificationType.Success));
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_SaveFail_Title".GetLocalized(), ex.Message, NotificationType.Error));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnCropBatchApplyClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            int[] sizes = { 512, 256, 128 };
            foreach (var size in sizes)
            {
                await SaveCroppedImageAsync(size);
            }
            ViewModel.UpdateAvatarPreview();
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Success".GetLocalized(), "Avatar_Batch_Success".GetLocalized(), NotificationType.Success));
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage("Avatar_Batch_Fail_Title".GetLocalized(), ex.Message, NotificationType.Error));
        }
        finally
        {
            deferral.Complete();
        }
    }
}
