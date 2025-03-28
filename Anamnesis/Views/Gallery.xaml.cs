﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Views;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Anamnesis.Core.Extensions;
using Anamnesis.Files;
using Anamnesis.Services;
using PropertyChanged;
using Serilog;
using XivToolsWpf;

/// <summary>
/// Interaction logic for Gallery.xaml.
/// </summary>
[AddINotifyPropertyChangedInterface]
public partial class Gallery : UserControl
{
	private const int ImageDelay = 5000;

	private bool isImage1 = true;
	private bool isRunning = false;
	private bool forceUpdate = false;
	private int currentIndex = 0;
	private Entry? currentEntry = null;

	public Gallery()
	{
		this.InitializeComponent();
		this.ContentArea.DataContext = this;

		SettingsService.SettingsChanged += this.OnSettingsChanged;
		this.OnSettingsChanged(null, null);
	}

	public bool CanSkip { get; set; } = false;
	public string? Image1Path { get; set; } = null;
	public string Image1Author { get; set; } = string.Empty;
	public string? Image2Path { get; set; } = null;
	public string Image2Author { get; set; } = string.Empty;

	private void OnSettingsChanged(object? sender, PropertyChangedEventArgs? e)
	{
		this.Visibility = SettingsService.Current.ShowGallery ? Visibility.Visible : Visibility.Hidden;

		if (SettingsService.Current.ShowGallery && !this.isRunning)
		{
			Task.Run(this.Run);
		}
	}

	private async Task Run()
	{
		if (this.isRunning)
			return;

		if (!SettingsService.Current.ShowGallery)
			return;

		this.isRunning = true;

		while (!this.IsVisible)
			await Task.Delay(500);

		Random rnd = new Random();

		this.forceUpdate = true;

		while (Application.Current != null && SettingsService.Current.ShowGallery)
		{
			List<Entry> entries;

			if (string.IsNullOrEmpty(SettingsService.Current.GalleryDirectory))
			{
				entries = EmbeddedFileUtility.Load<List<Entry>>("Data/Images.json");
			}
			else
			{
				string dir = FileService.ParseToFilePath(SettingsService.Current.GalleryDirectory);

				if (!Directory.Exists(dir))
				{
					await Dispatch.MainThread();
					SettingsService.Current.GalleryDirectory = null;
					continue;
				}

				string[] files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);

				entries = new List<Entry>();
				foreach (string filePath in files)
				{
					string ext = Path.GetExtension(filePath);
					if (ext != ".jpg" && ext != ".png" && ext != ".jpeg" && ext != ".bmp")
						continue;

					Entry entry = new Entry();
					entry.Url = filePath;
					entry.Author = Path.GetFileNameWithoutExtension(filePath);
					entries.Add(entry);
				}
			}

			Log.Information("Loading gallery image list");

			entries.Shuffle();

			while (this.isRunning)
			{
				while (!this.IsVisible)
					await Task.Delay(5000);

				if (!SettingsService.Current.ShowGallery)
				{
					this.isRunning = false;
					return;
				}

				int delay = 0;
				while (!this.forceUpdate && delay < ImageDelay)
				{
					delay += 100;
					await Task.Delay(100);
				}

				this.currentIndex++;

				if (this.currentIndex >= entries.Count)
					this.currentIndex = 0;

				if (this == null || entries == null || entries.Count == 0)
					return;

				await this.Show(entries[this.currentIndex], rnd);

				this.forceUpdate = false;
			}
		}
	}

	private async Task Show(Entry entry, Random rnd)
	{
		if (entry.Url == null || entry.Author == null)
			return;

		await Dispatch.MainThread();
		this.CanSkip = false;
		bool valid = true;

		await Dispatch.NonUiThread();

		string imagePath = entry.Thumbnail ?? entry.Url;
		valid = true;

		if (string.IsNullOrEmpty(SettingsService.Current.GalleryDirectory))
		{
			try
			{
				imagePath = await FileService.CacheRemoteImage(imagePath);

				// If CacheRemoteImage returned the Thumbnail URL, that means it was present but the download failed. Attempt to download full URL.
				if (imagePath.Equals(entry.Thumbnail))
				{
					Log.Verbose("Failed to load Image from Thumbnail, attemtping full URL.");
					imagePath = await FileService.CacheRemoteImage(entry.Url);
				}

				if (!UrlUtility.IsUrl(imagePath) && !FileService.VerifyImageIntegrity(imagePath).Result)
				{
					Log.Warning($"Failed to Verify Gallery Image, attempting to delete: {imagePath}");

					if (File.Exists(imagePath))
					{
						File.SetAttributes(imagePath, FileAttributes.Normal);
						File.Delete(imagePath);
					}

					valid = false;
				}
				else
				{
					Log.Verbose($"Successfully Loaded Valid Image {imagePath}");
				}
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to get remote image");
				valid = false;
			}
		}

		await Dispatch.MainThread();

		if (!valid)
		{
			this.CanSkip = true;
			return;
		}

		this.currentEntry = entry;

		UIElement oldHost = this.isImage1 ? this.Image1Host : this.Image2Host;

		this.isImage1 = !this.isImage1;
		UIElement host;
		Image image;
		RotateTransform rotate;

		if (this.isImage1)
		{
			this.Image1Path = imagePath;
			this.Image1Author = entry.Author;
			image = this.Image1;
			host = this.Image1Host;
			rotate = this.Image1Rotate;
		}
		else
		{
			this.Image2Path = imagePath;
			this.Image2Author = entry.Author;
			image = this.Image2;
			host = this.Image2Host;
			rotate = this.Image2Rotate;
		}

		rotate.Angle = -5 + (rnd.NextDouble() * 10.0);

		while (image.Source != null && !image.Source.CanFreeze)
			await Task.Delay(100);

		await Dispatch.MainThread();

		this.CanSkip = true;

		host.Opacity = 0.0;

		Storyboard? sb = this.Resources[this.isImage1 ? "StoryboardImage1" : "StoryboardImage2"] as Storyboard;

		if (sb == null)
			throw new System.Exception("Missing gallery storyboard");

		sb.SpeedRatio = this.forceUpdate ? 10 : 1;
		sb.Begin();

		await Task.Delay(this.forceUpdate ? 200 : 2000);

		oldHost.Opacity = 0.0;
	}

	private void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (this.currentEntry == null || this.currentEntry.Url == null)
			return;

		UrlUtility.Open(this.currentEntry.Url);
	}

	private void OnPrevClicked(object sender, RoutedEventArgs e)
	{
		if (this.currentIndex > 1)
		{
			this.currentIndex -= 2;
			this.forceUpdate = true;
		}
	}

	private void OnNextClicked(object sender, RoutedEventArgs e)
	{
		this.forceUpdate = true;
	}

	public class Entry
	{
		public string? Url { get; set; }
		public string? Author { get; set; }
		public string? Thumbnail { get; set; }
	}
}
