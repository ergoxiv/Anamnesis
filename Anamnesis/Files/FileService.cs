﻿// © Anamnesis.
// Licensed under the MIT license.

namespace Anamnesis.Files;

using Anamnesis;
using Anamnesis.GUI.Dialogs;
using Anamnesis.GUI.Views;
using Anamnesis.Services;
using Anamnesis.Utils;
using Microsoft.Win32;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public class FileService : ServiceBase<FileService>
{
	public static readonly string StoreDirectory = "%AppData%/Anamnesis/";
	public static readonly string CacheDirectory = "%AppData%/Anamnesis/RemoteCache/";

	private static readonly Dictionary<Type, string> TypeNameLookup = new Dictionary<Type, string>();
	private static readonly Dictionary<Type, FileFilter> FileTypeFilterLookup = new Dictionary<Type, FileFilter>();

	public static Shortcut Desktop => new Shortcut(
		new DirectoryInfo(ParseToFilePath("%Desktop%")),
		"Shortcuts/Desktop.png",
		"Shortcut_Desktop");

	public static Shortcut DefaultPoseDirectory => new Shortcut(
		new DirectoryInfo(ParseToFilePath(SettingsService.Current.DefaultPoseDirectory)),
		"Shortcuts/Anamnesis.png",
		"Shortcut_AnamnesisPose");

	public static Shortcut DefaultCharacterDirectory => new Shortcut(
		new DirectoryInfo(ParseToFilePath(SettingsService.Current.DefaultCharacterDirectory)),
		"Shortcuts/Anamnesis.png",
		"Shortcut_AnamnesisCharacter");

	public static Shortcut DefaultCameraDirectory => new Shortcut(
		new DirectoryInfo(ParseToFilePath(SettingsService.Current.DefaultCameraShotDirectory)),
		"Shortcuts/Anamnesis.png",
		"Shortcut_AnamnesisCamera");

	public static Shortcut DefaultSceneDirectory => new Shortcut(
		new DirectoryInfo(ParseToFilePath(SettingsService.Current.DefaultSceneDirectory)),
		"Shortcuts/Anamnesis.png",
		"Shortcut_AnamnesisScenes");

	public static Shortcut StandardPoseDirectory => new Shortcut(
		new DirectoryInfo(ParseToFilePath("%AppData%/Anamnesis/StandardPoses/")),
		"Shortcuts/AnamnesisBuiltIn.png",
		"Shortcut_BuiltInPose");

	public static Shortcut CMToolPoseSaveDir => new Shortcut(
		new DirectoryInfo(ParseToFilePath("%MyDocuments%/CMTool/Matrix Saves/")),
		"Shortcuts/cmtool.png",
		"Shortcut_CMToolPose");

	public static Shortcut CMToolAppearanceSaveDir => new Shortcut(
		new DirectoryInfo(ParseToFilePath("%MyDocuments%/CMTool/")),
		"Shortcuts/cmtool.png",
		"Shortcut_CMToolPose");

	public static Shortcut FFxivDatCharacterDirectory => new Shortcut(
		new DirectoryInfo(ParseToFilePath("%MyDocuments%/My Games/FINAL FANTASY XIV - A Realm Reborn/")),
		"Shortcuts/ffxiv.png",
		"Shortcut_FfxivAppearance");

	/// <summary>
	/// Replaces special folders (%ApplicationData%) with the actual path.
	/// </summary>
	public static string ParseToFilePath(string path)
	{
		foreach (Environment.SpecialFolder? specialFolder in Enum.GetValues<Environment.SpecialFolder>().Select(v => (Environment.SpecialFolder?)v))
		{
			if (specialFolder == null)
				continue;

			path = path.Replace($"%{specialFolder}%", Environment.GetFolderPath((Environment.SpecialFolder)specialFolder));
		}

		// Special case for "AppData" instead of "ApplicationData"
		path = path.Replace("%AppData%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
		path = path.Replace("%InstallDir%", Directory.GetCurrentDirectory());
		path = path.Replace('/', '\\');
		return path;
	}

	/// <summary>
	/// Replaces special fodler paths with special fodler notation (%appdata%).
	/// </summary>
	public static string ParseFromFilePath(string path)
	{
		// Special case for "AppData" instead of "ApplicationData"
		path = path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), $"%AppData%");

		foreach (Environment.SpecialFolder? specialFolder in Enum.GetValues<Environment.SpecialFolder>().Select(v => (Environment.SpecialFolder?)v))
		{
			if (specialFolder == null)
				continue;

			string specialPath = Environment.GetFolderPath((Environment.SpecialFolder)specialFolder);

			if (string.IsNullOrEmpty(specialPath))
				continue;

			path = path.Replace(specialPath, $"%{specialFolder}%");
		}

		path = path.Replace('\\', '/');
		return path;
	}

	public static void OpenDirectory(string path)
	{
		string dir = FileService.ParseToFilePath(path);
		Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", $"\"{dir}\"");
	}

	public static async Task<OpenResult> Open(DirectoryInfo? defaultDirectory, Shortcut[] shortcuts, Type[] fileTypes)
	{
		OpenResult result = default;

		try
		{
			bool useExplorerBrowser = SettingsService.Current.UseWindowsExplorer;

			if (!useExplorerBrowser)
			{
				List<FileFilter> filters = ToFileFilters(fileTypes);
				FileBrowserView browser = new FileBrowserView(shortcuts, filters, defaultDirectory, null, FileBrowserView.Modes.Load);
				await ViewService.ShowDrawer(browser);

				while (browser.IsOpen)
					await Task.Delay(10);

				result.Path = browser.FinalSelection as FileInfo;
				useExplorerBrowser = browser.UseFileBrowser;
			}

			if (useExplorerBrowser)
			{
				result.Path = await App.Current.Dispatcher.InvokeAsync<FileInfo?>(() =>
				{
					OpenFileDialog dlg = new OpenFileDialog();

					if (defaultDirectory == null)
					{
						Shortcut? defaultShortcut = null;
						foreach (Shortcut shortcut in shortcuts)
						{
							if (defaultDirectory == null && shortcut.Directory.Exists && defaultShortcut == null)
							{
								defaultDirectory = shortcut.Directory;
							}
						}
					}

					if (defaultDirectory != null)
						dlg.InitialDirectory = defaultDirectory.FullName;

					if (!Directory.Exists(dlg.InitialDirectory))
					{
						Log.Warning($"Initial directory of open dialog is invalid: {dlg.InitialDirectory}");
						dlg.InitialDirectory = null;
					}

					foreach (Shortcut? shortcut in shortcuts)
					{
						dlg.CustomPlaces.Add(new FileDialogCustomPlace(ParseToFilePath(shortcut.Path)));
					}

					dlg.Filter = ToAnyFilter(fileTypes);

					bool? result = dlg.ShowDialog();

					if (result != true)
						return null;

					return new FileInfo(dlg.FileName);
				});
			}

			if (result.Path == null)
				return result;

			result.File = Load(result.Path, fileTypes);
			return result;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to open file");
		}

		return result;
	}

	public static FileBase? Load(FileInfo info, Type fileType)
	{
		string extension = Path.GetExtension(info.FullName);

		try
		{
			FileBase? file = Activator.CreateInstance(fileType) as FileBase;

			if (file == null)
				throw new Exception($"Failed to create instance of file type: {fileType}");

			using FileStream stream = new FileStream(info.FullName, FileMode.Open);
			return file.Deserialize(stream);
		}
		catch (Exception ex)
		{
			Log.Verbose(ex, $"Attempted to deserialize file: {info} as type: {fileType} failed.");
			throw;
		}
	}

	public static FileBase? Load(FileInfo info, Type[] fileTypes)
	{
		string extension = Path.GetExtension(info.FullName);

		Exception? lastException = null;
		foreach (Type fileType in fileTypes)
		{
			FileFilter filter = GetFileTypeFilter(fileType);

			if (filter.Extension == extension)
			{
				try
				{
					FileBase? file = Activator.CreateInstance(fileType) as FileBase;

					if (file == null)
						throw new Exception($"Failed to create instance of file type: {fileType}");

					using FileStream stream = new FileStream(info.FullName, FileMode.Open);
					return file.Deserialize(stream);
				}
				catch (Exception ex)
				{
					Log.Verbose(ex, $"Attempted to deserialize file: {info} as type: {fileType} failed.");
					lastException = ex;
				}
			}
		}

		if (lastException != null)
			throw lastException;

		throw new Exception($"Unrecognised file: {info}");
	}

	public static async Task<SaveResult> Save<T>(DirectoryInfo? defaultDirectory, params Shortcut[] directories)
		where T : FileBase
	{
		SaveResult result = default;
		////result.Path = defaultPath;

		string ext = GetFileTypeFilter(typeof(T)).Extension;

		try
		{
			bool useExplorerBrowser = SettingsService.Current.UseWindowsExplorer;

			string typeName = GetFileTypeName(typeof(T));

			if (!useExplorerBrowser)
			{
				List<FileFilter> filters = new List<FileFilter>()
					{
						GetFileTypeFilter(typeof(T)),
					};

				FileBrowserView browser = new FileBrowserView(directories, filters, defaultDirectory, typeName, FileBrowserView.Modes.Save);
				await ViewService.ShowDrawer(browser);

				while (browser.IsOpen)
					await Task.Delay(10);

				result.Path = browser.FinalSelection as FileInfo;
				useExplorerBrowser = browser.UseFileBrowser;
			}

			if (useExplorerBrowser)
			{
				result.Path = await App.Current.Dispatcher.InvokeAsync<FileInfo?>(() =>
				{
					SaveFileDialog dlg = new SaveFileDialog();
					dlg.Filter = ToFilter(typeof(T));
					dlg.InitialDirectory = defaultDirectory?.FullName ?? string.Empty;

					if (!Directory.Exists(dlg.InitialDirectory))
					{
						Log.Warning($"Initial directory of save dialog is invalid: {dlg.InitialDirectory}");
						dlg.InitialDirectory = null;
					}

					bool? dlgResult = dlg.ShowDialog();

					if (dlgResult != true)
						return null;

					return new FileInfo(dlg.FileName);
				});
			}

			if (result.Path == null)
				return result;

			if (result.Path.Exists)
			{
				string fileName = Path.GetFileNameWithoutExtension(result.Path.FullName);
				bool? overwrite = await GenericDialog.ShowAsync(LocalizationService.GetStringFormatted("FileBrowser_ReplaceMessage", fileName), LocalizationService.GetString("FileBrowser_ReplaceTitle"), MessageBoxButton.YesNo);
				if (overwrite != true)
				{
					return await Save<T>(defaultDirectory, directories);
				}
			}

			DirectoryInfo? dir = result.Path.Directory;
			if (dir == null)
				throw new Exception("No directory in save path");

			if (!dir.Exists)
			{
				dir.Create();
			}
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to save file");
		}

		return result;
	}

	/// <summary>
	/// Caches remote file, returns path to file if successful or file exists or original url if unsuccessful. Allows to custom file names to avoid overwrite.
	/// </summary>
	public static async Task<string> CacheRemoteFile(string url, string? filePath)
	{
		string cacheDir = ParseToFilePath(CacheDirectory);

		if (!Directory.Exists(cacheDir))
		{
			Directory.CreateDirectory(cacheDir);
		}

		Uri uri = new Uri(url);

		string localFile;

		if (filePath != null)
		{
			localFile = cacheDir + filePath;

			string? directoryName = Path.GetDirectoryName(localFile);
			if (directoryName != null && !Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}
		}
		else
		{
			localFile = cacheDir + uri.Segments[uri.Segments.Length - 1];
		}

		if (!File.Exists(localFile))
		{
			try
			{
				using HttpClient client = new();
				HttpResponseMessage result = await client.GetAsync(url);

				if (!result.IsSuccessStatusCode)
				{
					Log.Verbose($"Failed to cache data from url: {url}. Status code: {result.StatusCode}");
					return url;
				}

				Stream responseStream = await result.Content.ReadAsStreamAsync();

				using FileStream fileStream = new FileStream(localFile, FileMode.Create, FileAccess.Write);
				responseStream.CopyTo(fileStream);

				Log.Verbose($"Cached remote file: {url}. {fileStream.Position} bytes.");
			}
			catch (Exception ex)
			{
				throw new Exception($"Failed to cache data from url: {url}", ex);
			}
		}

		return localFile;
	}

	/// <summary>
	/// Caches remote image, returns path to image if successful or image exists or original url if unsuccessful.
	/// Generates name based on hash of original url to avoid overwriting of images with same name.
	/// </summary>
	public static async Task<string> CacheRemoteImage(string url)
	{
		Uri uri = new(url);
		string imagePath = "ImageCache/" + HashUtility.GetHashString(url) + Path.GetExtension(uri.Segments[uri.Segments.Length - 1]);

		return await CacheRemoteFile(url, imagePath);
	}

	/// <summary>
	/// Verifies the integrity of an image file.
	/// </summary>
	/// <param name="path">The Path to the Image to be tested.</param>
	/// <returns>true if the image is Valid. false if not.</returns>
	public static async Task<bool> VerifyImageIntegrity(string path)
	{
		if (!File.Exists(path))
		{
			return false;
		}

		try
		{
			byte[] imageData = await File.ReadAllBytesAsync(path);
			using var ms = new MemoryStream(imageData);

			// 'validateImageData' will ensure the image data is properly validated.
			using var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: true);

			return true;
		}
		catch
		{
			// If an exception is thrown, the file is either not an image or is corrupted.
			return false;
		}
	}

	/// <summary>
	/// Recursively sets the attributes of all files and sub-directories in the directory to FileAttributes.Normal.
	/// </summary>
	/// <param name="directory">The directory to process.</param>
	public static void SetAttributesNormal(DirectoryInfo directory)
	{
		directory.Attributes = FileAttributes.Normal;

		foreach (var subDirectory in directory.GetDirectories())
		{
			SetAttributesNormal(subDirectory);
		}

		foreach (var file in directory.GetFiles())
		{
			file.Attributes = FileAttributes.Normal;
		}
	}

	private static string ToAnyFilter(params Type[] types)
	{
		StringBuilder builder = new StringBuilder();
		builder.Append("Any|");

		foreach (Type type in types)
			builder.Append("*" + GetFileTypeFilter(type).Extension + ";");

		foreach (Type type in types)
		{
			builder.Append("|");
			builder.Append(GetFileTypeName(type));
			builder.Append("|");
			builder.Append("*" + GetFileTypeFilter(type).Extension);
		}

		return builder.ToString();
	}

	private static string ToFilter(Type type)
	{
		StringBuilder builder = new StringBuilder();
		builder.Append(GetFileTypeName(type));
		builder.Append("|");
		builder.Append("*" + GetFileTypeFilter(type).Extension);
		return builder.ToString();
	}

	private static List<FileFilter> ToFileFilters(params Type[] types)
	{
		List<FileFilter> results = new();

		foreach (Type type in types)
			results.Add(GetFileTypeFilter(type));

		return results;
	}

	private static string GetFileTypeName(Type fileType)
	{
		string? name;
		if (!TypeNameLookup.TryGetValue(fileType, out name))
		{
			FileBase? file = Activator.CreateInstance(fileType) as FileBase;

			if (file == null)
				throw new Exception($"Failed to create instance of file type: {fileType}");

			name = file.TypeName;
			TypeNameLookup.Add(fileType, name);
		}

		return name;
	}

	private static FileFilter GetFileTypeFilter(Type fileType)
	{
		FileFilter? filter;
		if (!FileTypeFilterLookup.TryGetValue(fileType, out filter))
		{
			FileBase? file = Activator.CreateInstance(fileType) as FileBase;

			if (file == null)
				throw new Exception($"Failed to create instance of file type: {fileType}");

			filter = file.GetFilter();
			FileTypeFilterLookup.Add(fileType, filter);
		}

		return filter;
	}
}

#pragma warning disable SA1201, SA1402
public struct OpenResult
{
	public FileBase? File;
	public FileInfo? Path;

	public DirectoryInfo? Directory => this.Path?.Directory;
}

public struct SaveResult
{
	public FileInfo? Path;

	public DirectoryInfo? Directory => this.Path?.Directory;
}

public class Shortcut
{
	public DirectoryInfo Directory { get; private set; }
	public ImageSource? Icon { get; private set; }
	public string LabelKey { get; private set; }

	public string Path => FileService.ParseFromFilePath(this.Directory.FullName);

	public Shortcut(DirectoryInfo dir, string icon, string key)
	{
		this.Directory = dir;
		this.LabelKey = key;

		if (!string.IsNullOrEmpty(icon))
		{
			try
			{
				BitmapImage newIcon = new BitmapImage(new Uri($"pack://application:,,,/Anamnesis;component/Assets/{icon}"));
				newIcon.Freeze();
				this.Icon = newIcon;
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Failed to load icon for shortcut: {icon}");
			}
		}
	}
}
