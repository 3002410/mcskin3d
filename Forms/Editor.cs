﻿//
//    MCSkin3D, a 3d skin management studio for Minecraft
//    Copyright (C) 2011-2012 Altered Softworks & MCSkin3D Team
//
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using MCSkin3D.Controls;
using MCSkin3D.Forms;
using MCSkin3D.Language;
using MCSkin3D.Properties;
using MCSkin3D.Swatches;
using MultiPainter;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Paril.Compatibility;
using Paril.Components;
using Paril.Components.Shortcuts;
using Paril.Components.Update;
using Paril.Controls;
using Paril.Drawing;
using Paril.Extensions;
using Paril.Net;
using Paril.OpenGL;
using PopupControl;
using KeyPressEventArgs = System.Windows.Forms.KeyPressEventArgs;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using Timer = System.Timers.Timer;

namespace MCSkin3D
{
	public partial class Editor : Form
	{
		#region Variables

		private static readonly ShortcutEditor _shortcutEditor = new ShortcutEditor();
		private readonly List<BackgroundImage> _backgrounds = new List<BackgroundImage>();
		private readonly Dictionary<Size, Texture> _charPaintSizes = new Dictionary<Size, Texture>();
		private readonly UndoRedoPanel _redoListBox;
		private readonly List<ToolIndex> _tools = new List<ToolIndex>();
		private readonly UndoRedoPanel _undoListBox;
		private readonly Updater _updater;
		private readonly GLControl _rendererControl;

		private float _2DCamOffsetX;
		private float _2DCamOffsetY;
		private float _2DZoom = 8;
		private Vector3 _3DOffset = Vector3.Zero;
		private float _3DRotationX = 180, _3DRotationY;
		private float _3DZoom = -80;
		private Texture _alphaTex;
		private float _animationTime;
		private UndoBuffer _currentUndoBuffer;
		private ViewMode _currentViewMode = ViewMode.Perspective;
		private Texture _font;
		private Texture _grassTop;
		private Skin _lastSkin;
		private bool _mouseIsDown;
		private Point _mousePoint;
		internal PleaseWait _pleaseWaitForm;
		private Texture _previewPaint;
		private int _selectedBackground;
		private ToolIndex _selectedTool;
		private Texture _toolboxDownHover;
		private Texture _toolboxDownNormal;
		private Texture _toolboxUpHover;
		private Texture _toolboxUpNormal;

		#endregion

		// ===============================================
		// Private/Static variables
		// ===============================================

		private static bool _screenshotMode;

		public static Stopwatch _renderTimer = new Stopwatch();
		public static Stopwatch _sortTimer = new Stopwatch();
		public static Stopwatch _batchTimer = new Stopwatch();
		public static Stopwatch _compileTimer = new Stopwatch();
		private static bool _firstCalc;
		private static ToolStripMenuItem[] _antialiasOpts;
		private static Language.Language _currentLanguage;
		private readonly Timer _animTimer = new Timer(25);
		private readonly ImportSite _importFromSite = new ImportSite();
		private Rectangle _currentViewport;
		private BackgroundImage _dynamicOverlay;
		private bool _isValidPick;
		private bool _mouseIn3D;
		private string[] _newSkinDirs;
		private ModelToolStripMenuItem _oldModel;
		private bool _opening;
		private Point _pickPosition = new Point(-1, -1);
		private TreeNode _rightClickedNode;
		private bool _waitingForRestart;
		private Matrix4 _cameraMatrix;
		private Form _popoutForm;
		private Matrix4d _projectionMatrix;
		private Matrix4 _viewMatrix;

		public ToolIndex SelectedTool
		{
			get { return _selectedTool; }
		}

		public Renderer MeshRenderer { get; private set; }

		public DodgeBurnOptions DodgeBurnOptions { get; private set; }
		public DarkenLightenOptions DarkenLightenOptions { get; private set; }
		public PencilOptions PencilOptions { get; private set; }
		public FloodFillOptions FloodFillOptions { get; private set; }
		public NoiseOptions NoiseOptions { get; private set; }
		public EraserOptions EraserOptions { get; private set; }
		public StampOptions StampOptions { get; private set; }

		public static Editor MainForm { get; private set; }

		// ===============================================
		// Constructor
		// ===============================================

		public GLControl Renderer
		{
			get { return _rendererControl; }
		}

		public MouseButtons CameraRotate
		{
			get
			{
				if (_selectedTool == _tools[(int) Tools.Camera])
					return MouseButtons.Left;
				else
					return MouseButtons.Right;
			}
		}

		public MouseButtons CameraZoom
		{
			get
			{
				if (_selectedTool == _tools[(int) Tools.Camera])
					return MouseButtons.Right;
				else
					return MouseButtons.Middle;
			}
		}

		public ColorPanel ColorPanel
		{
			get { return colorPanel; }
		}

		public static Model CurrentModel
		{
			get { return MainForm._lastSkin == null ? null : MainForm._lastSkin.Model; }
		}

		public float ToolScale
		{
			get
			{
				const float baseSize = 200.0f;

				return baseSize / _rendererControl.Size.Width;
			}
		}

		public static Language.Language CurrentLanguage
		{
			get { return _currentLanguage; }
			set
			{
				if (_currentLanguage != null)
					_currentLanguage.Item.Checked = false;

				_currentLanguage = value;
				GlobalSettings.LanguageFile = _currentLanguage.Culture.Name;
				MainForm.languageProvider1.LanguageChanged(value);
				MainForm.DarkenLightenOptions.languageProvider1.LanguageChanged(value);
				MainForm.PencilOptions.languageProvider1.LanguageChanged(value);
				MainForm.DodgeBurnOptions.languageProvider1.LanguageChanged(value);
				MainForm.FloodFillOptions.languageProvider1.LanguageChanged(value);
				MainForm.ColorPanel.languageProvider1.LanguageChanged(value);
				MainForm.ColorPanel.SwatchContainer.languageProvider1.LanguageChanged(value);
				MainForm.login.languageProvider1.LanguageChanged(value);
				MainForm.NoiseOptions.languageProvider1.LanguageChanged(value);
				MainForm.EraserOptions.languageProvider1.LanguageChanged(value);
				MainForm.StampOptions.languageProvider1.LanguageChanged(value);
				MainForm._importFromSite.languageProvider1.LanguageChanged(value);
				MainForm.colorPanel.languageProvider1.LanguageChanged(value);
				MainForm.colorPanel.SwatchContainer.languageProvider1.LanguageChanged(value);

				if (MainForm._selectedTool != null)
					MainForm.toolStripStatusLabel1.Text = MainForm._selectedTool.Tool.GetStatusLabelText();

				_currentLanguage.Item.Checked = true;
			}
		}

		public static Vector3 CameraPosition { get; private set; }

		public static float GrassY { get; private set; }

		public static string RootFolderString
		{
			get
			{
				if (HasOneRoot)
					return Path.GetDirectoryName(GlobalSettings.SkinDirectories[0]);

				throw new InvalidOperationException();
			}
		}

		public static bool HasOneRoot
		{
			get { return GlobalSettings.SkinDirectories.Length == 1; }
		}

		public void PerformResetCamera()
		{
			_2DCamOffsetX = 0;
			_2DCamOffsetY = 0;
			_2DZoom = 8;
			_3DZoom = -80;
			_3DRotationX = 180;
			_3DRotationY = 0;
			_3DOffset = Vector3.Zero;

			CalculateMatrices();
			_rendererControl.Invalidate();
		}

		// =====================================================================
		// Updating
		// =====================================================================

		// =====================================================================
		// Shortcuts
		// =====================================================================

		// =====================================================================
		// Overrides
		// =====================================================================

		// =====================================================================
		// Private functions
		// =====================================================================

		private void rendererControl_Load(object sender, EventArgs e)
		{
			_rendererControl.Invalidate();
			GL.ClearColor(GlobalSettings.BackgroundColor);

			GL.Enable(EnableCap.Texture2D);
			GL.ShadeModel(ShadingModel.Smooth); // Enable Smooth Shading
			GL.Enable(EnableCap.DepthTest); // Enables Depth Testing
			GL.DepthFunc(DepthFunction.Lequal); // The Type Of Depth Testing To Do
			GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest); // Really Nice Perspective Calculations
			GL.Enable(EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int) TextureEnvMode.Modulate);
			GL.Enable(EnableCap.CullFace);

			_toolboxUpNormal = new TextureGL(Resources.buttong);
			_toolboxUpHover = new TextureGL(Resources.buttong_2);
			_toolboxDownNormal = new TextureGL(Resources.buttong_down);
			_toolboxDownHover = new TextureGL(Resources.buttong_down2);

			_font = new TextureGL(Resources.tinyfont);
			_font.SetMipmapping(false);
			_font.SetRepeat(false);

			_grassTop = new TextureGL("grass.png");
			_grassTop.SetMipmapping(false);
			_grassTop.SetRepeat(true);

			_dynamicOverlay = new BackgroundImage("Dynamic", "Dynamic", null);
			_dynamicOverlay.Item = mDYNAMICOVERLAYToolStripMenuItem;
			_backgrounds.Add(_dynamicOverlay);

			foreach (string file in Directory.GetFiles("Overlays", "*.png"))
			{
				try
				{
					var image = new TextureGL(file);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Clamp);
					GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Clamp);

					_backgrounds.Add(new BackgroundImage(file, Path.GetFileNameWithoutExtension(file), image));
				}
				catch
				{
					MessageBox.Show(this, string.Format(GetLanguageString("B_MSG_OVERLAYERROR"), file));
				}
			}

			int index = 0;
			foreach (BackgroundImage b in _backgrounds)
			{
				ToolStripMenuItem item = b.Item ?? new ToolStripMenuItem(b.Name);
				b.Item = item;

				if (b.Path == GlobalSettings.LastBackground)
				{
					item.Checked = true;
					_selectedBackground = index;
				}

				item.Click += item_Clicked;
				item.Tag = index++;

				if (!backgroundsToolStripMenuItem.DropDownItems.Contains(item))
					backgroundsToolStripMenuItem.DropDownItems.Add(item);
			}

			_previewPaint = new TextureGL();
			GlobalDirtiness.CurrentSkin = new TextureGL();
			_alphaTex = new TextureGL();

			unsafe
			{
				var arra = new byte[64 * 32];
				_previewPaint.Upload(arra, 64, 32);
				_previewPaint.SetMipmapping(false);
				_previewPaint.SetRepeat(false);

				GlobalDirtiness.CurrentSkin.Upload(arra, 64, 32);
				GlobalDirtiness.CurrentSkin.SetMipmapping(false);
				GlobalDirtiness.CurrentSkin.SetRepeat(false);

				arra = new byte[4 * 4 * 4];
				fixed (byte* texData = arra)
				{
					byte* d = texData;

					for (int y = 0; y < 4; ++y)
					{
						for (int x = 0; x < 4; ++x)
						{
							bool dark = ((x + (y & 1)) & 1) == 1;

							if (dark)
								*((int*) d) = (80 << 0) | (80 << 8) | (80 << 16) | (255 << 24);
							else
								*((int*) d) = (127 << 0) | (127 << 8) | (127 << 16) | (255 << 24);
							d += 4;
						}
					}
				}

				_alphaTex.Upload(arra, 4, 4);
				_alphaTex.SetMipmapping(false);
				_alphaTex.SetRepeat(true);
			}

			if (GL.GetString(StringName.Extensions).Contains("GL_EXT_vertex_array"))
				MeshRenderer = new ClientArrayRenderer();
			else
				MeshRenderer = new ImmediateRenderer();
		}

		private void item_Clicked(object sender, EventArgs e)
		{
			var item = (ToolStripMenuItem) sender;
			GlobalSettings.LastBackground = item.Text;
			_backgrounds[_selectedBackground].Item.Checked = false;
			_selectedBackground = (int) item.Tag;
			item.Checked = true;
		}

		private Texture GetPaintTexture(int width, int height)
		{
			if (!_charPaintSizes.ContainsKey(new Size(width, height)))
			{
				var tex = new TextureGL();

				var arra = new int[width * height];
				unsafe
				{
					fixed (int* texData = arra)
					{
						int* d = texData;

						for (int y = 0; y < height; ++y)
						{
							for (int x = 0; x < width; ++x)
							{
								*d = ((y * width) + x) | (255 << 24);
								d++;
							}
						}
					}
				}

				tex.Upload(arra, width, height);
				tex.SetMipmapping(false);
				tex.SetRepeat(false);

				_charPaintSizes.Add(new Size(width, height), tex);

				return tex;
			}

			return _charPaintSizes[new Size(width, height)];
		}

		private void DrawSkinnedRectangle
			(float x, float y, float z, float width, float length, float height,
			 int topSkinX, int topSkinY, int topSkinW, int topSkinH,
			 Texture texture, int skinW = 64, int skinH = 32)
		{
			texture.Bind();

			GL.Begin(BeginMode.Quads);

			width /= 2;
			length /= 2;
			height /= 2;

			float tsX = (float) topSkinX / skinW;
			float tsY = (float) topSkinY / skinH;
			float tsW = (float) topSkinW / skinW;
			float tsH = (float) topSkinH / skinH;

			GL.TexCoord2(tsX, tsY + tsH - 0.00005);
			GL.Vertex3(x - width, y + length, z + height); // Bottom Right Of The Quad (Top)
			GL.TexCoord2(tsX + tsW - 0.00005, tsY + tsH - 0.00005);
			GL.Vertex3(x + width, y + length, z + height); // Bottom Left Of The Quad (Top)
			GL.TexCoord2(tsX + tsW - 0.00005, tsY);
			GL.Vertex3(x + width, y + length, z - height); // Top Left Of The Quad (Top)
			GL.TexCoord2(tsX, tsY);
			GL.Vertex3(x - width, y + length, z - height); // Top Right Of The Quad (Top)

			GL.End();
		}

		private void DrawCharacter2D(Texture font, byte c, float xOfs, float yOfs, float width, float height)
		{
			font.Bind();

			float tx = (((c - 32) % 16) * 8) / 128.0f;
			float ty = (((c - 32) / 16) * 8) / 64.0f;
			const float txw = 8.0f / 128.0f;
			const float txh = 8.0f / 64.0f;

			GL.Begin(BeginMode.Quads);
			GL.TexCoord2(tx, ty);
			GL.Vertex2(xOfs, yOfs);
			GL.TexCoord2(tx + txw, ty);
			GL.Vertex2(xOfs + width, yOfs);
			GL.TexCoord2(tx + txw, ty + txh);
			GL.Vertex2(xOfs + width, height + yOfs);
			GL.TexCoord2(tx, ty + txh);
			GL.Vertex2(xOfs, height + yOfs);
			GL.End();
		}

		private void DrawString(Texture font, string s, float spacing, float size)
		{
			float x = 0;
			foreach (char c in s)
			{
				DrawCharacter2D(font, (byte) c, x, 0, size, size);
				x += spacing;
			}

			TextureGL.Unbind();
		}

		private void DrawStringWithinRectangle(Texture font, RectangleF rect, string s, float spacing, float size)
		{
			float x = rect.X;
			float y = rect.Y;
			foreach (char c in s)
			{
				DrawCharacter2D(font, (byte) c, x, y, size, size);
				x += spacing;

				if ((x + spacing) > rect.X + rect.Width)
				{
					x = rect.X;
					y += spacing;
				}

				if ((y + spacing) > rect.Y + rect.Height)
					break;
			}

			TextureGL.Unbind();
		}

		private void DrawPlayer2D(Texture tex, Skin skin, bool pickView)
		{
			if (!pickView && GlobalSettings.AlphaCheckerboard)
			{
				_alphaTex.Bind();

				GL.Begin(BeginMode.Quads);
				GL.TexCoord2(0, 0);
				GL.Vertex2(0, 0);
				GL.TexCoord2(_currentViewport.Width / 32.0f, 0);
				GL.Vertex2(_currentViewport.Width, 0);
				GL.TexCoord2(_currentViewport.Width / 32.0f, _currentViewport.Height / 32.0f);
				GL.Vertex2(_currentViewport.Width, _currentViewport.Height);
				GL.TexCoord2(0, _currentViewport.Height / 32.0f);
				GL.Vertex2(0, _currentViewport.Height);
				GL.End();
			}

			if (skin != null)
				tex.Bind();

			GL.PushMatrix();

			GL.Translate((_2DCamOffsetX), (_2DCamOffsetY), 0);
			GL.Translate((_currentViewport.Width / 2) + -_2DCamOffsetX, (_currentViewport.Height / 2) + -_2DCamOffsetY, 0);
			GL.Scale(_2DZoom, _2DZoom, 1);

			if (pickView)
				GL.Disable(EnableCap.Blend);
			else
				GL.Enable(EnableCap.Blend);

			GL.Translate((_2DCamOffsetX), (_2DCamOffsetY), 0);
			if (skin != null)
			{
				float w = skin.Width;
				float h = skin.Height;
				GL.Begin(BeginMode.Quads);
				GL.TexCoord2(0, 0);
				GL.Vertex2(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
				GL.TexCoord2(1, 0);
				GL.Vertex2((CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
				GL.TexCoord2(1, 1);
				GL.Vertex2((CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
				GL.TexCoord2(0, 1);
				GL.Vertex2(-(CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
				GL.End();
			}

			if (!pickView)
			{
				// Grid test
				TextureGL.Unbind();

				GL.Color4(GlobalSettings.DynamicOverlayGridColor);
				GL.PushMatrix();
				GL.Translate(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2), 0);
				GL.Begin(BeginMode.Lines);

				float wX = skin.Width / CurrentModel.DefaultWidth;
				float wY = skin.Height / CurrentModel.DefaultHeight;

				for (int i = 0; i <= skin.Width; ++i)
				{
					GL.Vertex2(i / wX, 0);
					GL.Vertex2(i / wX, skin.Height / wY);
				}

				for (int i = 0; i <= skin.Height; ++i)
				{
					GL.Vertex2(0, i / wY);
					GL.Vertex2(skin.Width / wX, i / wY);
				}

				GL.End();
				GL.PopMatrix();

				if (GlobalSettings.TextureOverlay && skin != null)
				{
					if (_backgrounds[_selectedBackground] == _dynamicOverlay)
					{
						GL.PushMatrix();
						GL.Translate(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2), 0);

						float stub = (GlobalSettings.DynamicOverlayLineSize / _2DZoom);
						float one = (1.0f / _2DZoom);

						var done = new List<RectangleF>();
						foreach (Mesh mesh in CurrentModel.Meshes)
						{
							foreach (Face face in mesh.Faces)
							{
								RectangleF toint = face.TexCoordsToFloat((int) CurrentModel.DefaultWidth, (int) CurrentModel.DefaultHeight);

								if (toint.Width == 0 ||
								    toint.Height == 0)
									continue;
								if (done.Contains(toint))
									continue;

								done.Add(toint);

								GL.Color4(GlobalSettings.DynamicOverlayLineColor);
								GL.Begin(BeginMode.Quads);
								GL.Vertex2(toint.X, toint.Y);
								GL.Vertex2(toint.X + toint.Width, toint.Y);
								GL.Vertex2(toint.X + toint.Width, toint.Y + stub);
								GL.Vertex2(toint.X, toint.Y + stub);

								GL.Vertex2(toint.X, toint.Y);
								GL.Vertex2(toint.X + stub, toint.Y);
								GL.Vertex2(toint.X + stub, toint.Y + toint.Height);
								GL.Vertex2(toint.X, toint.Y + toint.Height);

								GL.Vertex2(toint.X + toint.Width + one, toint.Y);
								GL.Vertex2(toint.X + toint.Width + one, toint.Y + toint.Height);
								GL.Vertex2(toint.X + toint.Width + one - stub, toint.Y + toint.Height);
								GL.Vertex2(toint.X + toint.Width + one - stub, toint.Y);

								GL.Vertex2(toint.X, toint.Y + toint.Height + one);
								GL.Vertex2(toint.X, toint.Y + toint.Height + one - stub);
								GL.Vertex2(toint.X + toint.Width, toint.Y + toint.Height + one - stub);
								GL.Vertex2(toint.X + toint.Width, toint.Y + toint.Height + one);
								GL.End();
								GL.Color4(Color.White);

								GL.Color4(GlobalSettings.DynamicOverlayTextColor);
								DrawStringWithinRectangle(_font, toint, mesh.Name + " " + Model.SideFromNormal(face.Normal),
								                          (6 * GlobalSettings.DynamicOverlayTextSize) / _2DZoom,
								                          (8.0f * GlobalSettings.DynamicOverlayTextSize) / _2DZoom);
								GL.Color4(Color.White);
							}
						}

						GL.PopMatrix();
					}
					else
					{
						_backgrounds[_selectedBackground].GLImage.Bind();

						GL.Begin(BeginMode.Quads);
						GL.TexCoord2(0, 0);
						GL.Vertex2(-(CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
						GL.TexCoord2(1, 0);
						GL.Vertex2((CurrentModel.DefaultWidth / 2), -(CurrentModel.DefaultHeight / 2));
						GL.TexCoord2(1, 1);
						GL.Vertex2((CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
						GL.TexCoord2(0, 1);
						GL.Vertex2(-(CurrentModel.DefaultWidth / 2), (CurrentModel.DefaultHeight / 2));
						GL.End();
					}
				}
			}

			GL.PopMatrix();

			GL.Disable(EnableCap.Blend);
		}

		private void DrawPlayer(Texture tex, Skin skin, bool pickView)
		{
			TextureGL.Unbind();
			bool grass = !pickView && grassToolStripMenuItem.Checked;

			Point clPt = _rendererControl.PointToClient(Cursor.Position);
			int x = clPt.X - (_currentViewport.Width / 2);
			int y = clPt.Y - (_currentViewport.Height / 2);

			if (!pickView && GlobalSettings.Transparency == TransparencyMode.All)
				GL.Enable(EnableCap.Blend);
			else
				GL.Disable(EnableCap.Blend);

			if (grass)
				DrawSkinnedRectangle(0, GrassY, 0, 1024, 0, 1024, 0, 0, 1024, 1024, _grassTop, 16, 16);

			Vector3 helmetRotate = (GlobalSettings.FollowCursor) ? new Vector3((float) y / 25, (float) x / 25, 0) : Vector3.Zero;
			double sinAnim = (GlobalSettings.Animate) ? Math.Sin(_animationTime) : 0;

			// add meshes
			if (GlobalSettings.RenderBenchmark && Editor.IsRendering)
				_compileTimer.Start();

			if (CurrentModel != null)
			{
				int meshIndex = -1;
				foreach (Mesh mesh in CurrentModel.Meshes)
				{
					meshIndex++;
					bool meshVisible = CurrentModel.PartsEnabled[meshIndex];

					if (meshVisible == false &&
					    !(GlobalSettings.Ghost && !pickView))
						continue;

					mesh.HasTransparency = _lastSkin.TransparentParts[meshIndex];
					mesh.Texture = tex;

					if (mesh.FollowCursor && GlobalSettings.FollowCursor)
						mesh.Rotate = helmetRotate;

					if (meshVisible == false && GlobalSettings.Ghost && !pickView)
					{
						foreach (Face f in mesh.Faces)
						{
							for (int i = 0; i < f.Colors.Length; ++i)
								f.Colors[i] = new Color4(1, 1, 1, 0.25f);
						}
					}
					else
					{
						foreach (Face f in mesh.Faces)
						{
							for (int i = 0; i < f.Colors.Length; ++i)
								f.Colors[i] = Color4.White;
						}
					}

					if (GlobalSettings.Animate && mesh.RotateFactor != 0)
						mesh.Rotate += new Vector3((float)sinAnim * mesh.RotateFactor, 0, 0);

					MeshRenderer.AddMesh(mesh);
				}
			}

			if (GlobalSettings.RenderBenchmark && Editor.IsRendering)
				_compileTimer.Stop();

			if (!pickView)
				MeshRenderer.Render();
			else
				MeshRenderer.RenderWithoutTransparency();
		}

		private void SetPreview()
		{
			if (_lastSkin == null)
			{
				var preview = new ColorGrabber(_previewPaint, 64, 32);
				preview.Save();
			}
			else
			{
				Skin skin = _lastSkin;

				var currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);

				bool pick = GetPick(_mousePoint.X, _mousePoint.Y, ref _pickPosition);

				//if (pick)
				{
					currentSkin.Load();
					if (_selectedTool.Tool.RequestPreview(ref currentSkin, skin, _pickPosition.X, _pickPosition.Y))
					{
						currentSkin.Texture = _previewPaint;
						currentSkin.Save();
					}
					else
					{
						currentSkin.Texture = _previewPaint;
						currentSkin.Save();
					}
				}
				/*else
				{
					currentSkin.Texture = _lastSkin.GLImage;
					currentSkin.Load();
					currentSkin.Texture = _previewPaint;
					currentSkin.Save();
				}*/
			}
		}

		public bool GetPick(int x, int y, ref Point hitPixel)
		{
			if (x < 0 || y < 0
			    || x > _rendererControl.Width || y > _rendererControl.Height)
			{
				hitPixel = new Point(-1, -1);
				return false;
			}

			_rendererControl.MakeCurrent();

			GL.ClearColor(Color.White);
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.ClearColor(GlobalSettings.BackgroundColor);

			Skin skin = _lastSkin;

			if (skin == null)
				return false;

			if (_currentViewMode == ViewMode.Perspective)
			{
				Setup3D(new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height));
				DrawPlayer(GetPaintTexture(skin.Width, skin.Height), skin, true);
			}
			else if (_currentViewMode == ViewMode.Orthographic)
			{
				Setup2D(new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height));
				DrawPlayer2D(GetPaintTexture(skin.Width, skin.Height), skin, true);
			}
			else
			{
				var halfHeight = (int) Math.Ceiling(_rendererControl.Height / 2.0f);

				Setup3D(new Rectangle(0, 0, _rendererControl.Width, halfHeight));
				DrawPlayer(GetPaintTexture(skin.Width, skin.Height), skin, true);

				Setup2D(new Rectangle(0, halfHeight, _rendererControl.Width, halfHeight));
				DrawPlayer2D(GetPaintTexture(skin.Width, skin.Height), skin, true);
			}

			GL.Flush();

			var pixel = new byte[4];

			GL.ReadPixels(x, _rendererControl.Height - y, 1, 1,
			              PixelFormat.Rgb, PixelType.UnsignedByte, pixel);

			uint pixVal = BitConverter.ToUInt32(pixel, 0);

			if (pixVal != 0xFFFFFF)
			{
				hitPixel = new Point((int) (pixVal % skin.Width), (int) (pixVal / skin.Width));
				return true;
			}

			hitPixel = new Point(-1, -1);
			return false;
		}

		private void animTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			_animationTime += 0.24f;
			_rendererControl.Invalidate();
		}

		private void rendererControl_MouseWheel(object sender, MouseEventArgs e)
		{
			CheckMouse(e.Y);

			if (_currentViewMode == ViewMode.Perspective || (_currentViewMode == ViewMode.Hybrid && _mouseIn3D))
			{
				_3DZoom += e.Delta / 50;

				CalculateMatrices();
				_rendererControl.Invalidate();
			}
			else
				_2DZoom += e.Delta / 50;

			if (_2DZoom < 0.25f)
				_2DZoom = 0.25f;
		}

		private void DrawGLToolbar()
		{
			// 2D
			Setup2D(new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height));
			TextureGL.Unbind();
			GL.Enable(EnableCap.Blend);

			float halfWidth = _rendererControl.Width / 2.0f;
			float halfImgWidth = 56.0f / 2.0f;

			var rect = new RectangleF(halfWidth - halfImgWidth, 0, halfImgWidth * 2, 22);

			Texture img = (splitContainer4.SplitterDistance == 0) ? _toolboxDownNormal : _toolboxUpNormal;

			if (rect.Contains(_mousePoint))
				GL.Color4((byte) 255, (byte) 255, (byte) 255, (byte) 255);
			else
				GL.Color4((byte) 255, (byte) 255, (byte) 255, (byte) 64);

			img.Bind();

			const float widSep = 56.0f / 64.0f;
			const float heiSep = 22.0f / 32.0f;

			GL.Begin(BeginMode.Quads);
			GL.TexCoord2(0, 0);
			GL.Vertex2(halfWidth - halfImgWidth, -1);
			GL.TexCoord2(widSep, 0);
			GL.Vertex2(halfWidth + halfImgWidth, -1);
			GL.TexCoord2(widSep, heiSep);
			GL.Vertex2(halfWidth + halfImgWidth, 21);
			GL.TexCoord2(0, heiSep);
			GL.Vertex2(halfWidth - halfImgWidth, 21);
			GL.End();
		}

		public Rectangle GetViewport3D()
		{
			if (_currentViewMode == ViewMode.Perspective)
				return new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height);
			else
			{
				var halfHeight = (int) Math.Ceiling(_rendererControl.Height / 2.0f);
				return new Rectangle(0, 0, _rendererControl.Width, halfHeight);
			}
		}

		public static bool IsRendering { get; private set; }

		static uint frameCount = 0;
		static TimeSpan _compileSpan = TimeSpan.MinValue,
						_batchSpan = TimeSpan.MinValue,
						_sortSpan = TimeSpan.MinValue,
						_renderSpan = TimeSpan.MinValue;

		private void rendererControl_Paint(object sender, PaintEventArgs e)
		{
			IsRendering = true;
			if (GlobalSettings.RenderBenchmark)
				_renderTimer.Start();

			_rendererControl.MakeCurrent();

			_mousePoint = _rendererControl.PointToClient(MousePosition);

			SetPreview();

			//GL.ClearColor(GlobalSettings.BackgroundColor);
			GL.Color4((byte) 255, (byte) 255, (byte) 255, (byte) 255);

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			Skin skin = _lastSkin;

			GL.PushMatrix();

			if (_currentViewMode == ViewMode.Perspective)
			{
				Setup3D(new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height));
				DrawPlayer(_previewPaint, skin, false);
			}
			else if (_currentViewMode == ViewMode.Orthographic)
			{
				Setup2D(new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height));
				DrawPlayer2D(_previewPaint, skin, false);
			}
			else
			{
				var halfHeight = (int) Math.Ceiling(_rendererControl.Height / 2.0f);

				Setup3D(new Rectangle(0, 0, _rendererControl.Width, halfHeight));
				DrawPlayer(_previewPaint, skin, false);

				Setup2D(new Rectangle(0, halfHeight, _rendererControl.Width, halfHeight));
				DrawPlayer2D(_previewPaint, skin, false);
			}

			GL.PopMatrix();

			_renderTimer.Stop();

			frameCount++;

			if (GlobalSettings.RenderBenchmark)
			{
				if (((++frameCount) % 60) == 0)
				{
					_compileSpan = TimeSpan.FromTicks(_compileTimer.Elapsed.Ticks / 60);
					_batchSpan = TimeSpan.FromTicks(_batchTimer.Elapsed.Ticks / 60);
					_sortSpan = TimeSpan.FromTicks(_sortTimer.Elapsed.Ticks / 60);
					_renderSpan = TimeSpan.FromTicks(_renderTimer.Elapsed.Ticks / 60);

					_sortTimer.Reset();
					_batchTimer.Reset();
					_compileTimer.Reset();

					_renderTimer.Reset();
				}

				GL.PushMatrix();
				GL.Enable(EnableCap.Blend);

				Setup2D(new Rectangle(0, 0, _rendererControl.Width, _rendererControl.Height));

				DrawString(_font, "Compile Time: " + _compileSpan.ToString(), 6, 8);
				GL.Translate(0, 8, 0);
				DrawString(_font, "Batch Time: " + _batchSpan.ToString(), 6, 8);
				GL.Translate(0, 8, 0);
				DrawString(_font, "Sort Time: " + _sortSpan.ToString(), 6, 8);
				GL.Translate(0, 8, 0);
				DrawString(_font, "Total Frame Time: " + _renderSpan.ToString(), 6, 8);
				GL.PopMatrix();
			}

			if (!_screenshotMode)
				DrawGLToolbar();

			if (!_screenshotMode)
				_rendererControl.SwapBuffers();
			IsRendering = false;
		}

		private void CalculateMatrices()
		{
			Rectangle viewport = GetViewport3D();
			_projectionMatrix = Matrix4d.Perspective(45, viewport.Width / (double) viewport.Height, 0.01, 100000);

			Bounds3 vec = Bounds3.EmptyBounds;
			Bounds3 allBounds = Bounds3.EmptyBounds;
			int count = 0;

			if (CurrentModel != null)
			{
				int meshIndex = 0;
				foreach (Mesh mesh in CurrentModel.Meshes)
				{
					allBounds += mesh.Bounds;

					if (CurrentModel.PartsEnabled[meshIndex])
					{
						vec += mesh.Bounds;
						count++;
					}

					meshIndex++;
				}
			}

			if (count == 0)
				vec.Mins = vec.Maxs = allBounds.Mins = allBounds.Maxs = Vector3.Zero;

			GrassY = allBounds.Maxs.Y;
			Vector3 center = vec.Center;

			_viewMatrix =
				Matrix4.CreateTranslation(-center.X + _3DOffset.X, -center.Y + _3DOffset.Y, -center.Z + _3DOffset.Z) *
				Matrix4.CreateFromAxisAngle(new Vector3(0, -1, 0), MathHelper.DegreesToRadians(_3DRotationY)) *
				Matrix4.CreateFromAxisAngle(new Vector3(1, 0, 0), MathHelper.DegreesToRadians(_3DRotationX)) *
				Matrix4.CreateTranslation(0, 0, _3DZoom);

			_cameraMatrix = _viewMatrix;
			_cameraMatrix.Invert();

			CameraPosition = Vector3.TransformPosition(Vector3.Zero, _cameraMatrix);
		}

		private void Setup3D(Rectangle viewport)
		{
			_currentViewport = viewport;

			if (!_firstCalc)
			{
				CalculateMatrices();
				_firstCalc = true;
			}

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.Viewport(viewport);
			Matrix4d mat = _projectionMatrix;
			GL.MultMatrix(ref mat);

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			GL.LoadMatrix(ref _viewMatrix);
		}

		private void Setup2D(Rectangle viewport)
		{
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.Viewport(viewport);
			GL.Ortho(0, viewport.Width, viewport.Height, 0, -1, 1);
			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();

			_currentViewport = viewport;
		}

		private void rendererControl_Resize(object sender, EventArgs e)
		{
			_rendererControl.MakeCurrent();

			CalculateMatrices();
			_rendererControl.Invalidate();
		}

		private void _animTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			if (_opening)
			{
				splitContainer4.SplitterDistance += splitContainer4.SplitterIncrement;

				if (splitContainer4.SplitterDistance >= 74)
				{
					splitContainer4.SplitterDistance = 74;
					_animTimer.Stop();
				}
			}
			else
			{
				if ((splitContainer4.SplitterDistance - splitContainer4.SplitterIncrement) <= splitContainer4.Panel1MinSize)
				{
					splitContainer4.SplitterDistance = splitContainer4.Panel1MinSize;
					_animTimer.Stop();
					return;
				}

				splitContainer4.SplitterDistance -= splitContainer4.SplitterIncrement;
			}
		}

		public void RotateView(Point delta, float factor)
		{
			if (_currentViewMode == ViewMode.Perspective || (_currentViewMode == ViewMode.Hybrid && _mouseIn3D))
			{
				_3DRotationY += (delta.X * ToolScale) * factor;
				_3DRotationX += (delta.Y * ToolScale) * factor;

				CalculateMatrices();
				_rendererControl.Invalidate();
			}
			else
			{
				_2DCamOffsetX += delta.X / _2DZoom;
				_2DCamOffsetY += delta.Y / _2DZoom;
			}
		}

		public void ScaleView(Point delta, float factor)
		{
			if (_currentViewMode == ViewMode.Perspective || (_currentViewMode == ViewMode.Hybrid && _mouseIn3D))
			{
				_3DZoom += (-delta.Y * ToolScale) * factor;

				CalculateMatrices();
				_rendererControl.Invalidate();
			}
			else
			{
				_2DZoom += -delta.Y / 25.0f;

				if (_2DZoom < 0.25f)
					_2DZoom = 0.25f;
			}
		}

		private void CheckMouse(int y)
		{
			if (y > (_rendererControl.Height / 2))
				_mouseIn3D = true;
			else
				_mouseIn3D = false;
		}

		public void SetPartTransparencies()
		{
			var grabber = new ColorGrabber(GlobalDirtiness.CurrentSkin, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();

			var paintedParts = new Dictionary<int, bool>();

			foreach (var p in _paintedPixels)
			{
				List<int> parts = CurrentModel.GetIntersectingParts(p.Key, _lastSkin);

				foreach (int part in parts)
				{
					if (!paintedParts.ContainsKey(part))
						paintedParts.Add(part, true);
				}
			}

			foreach (var p in paintedParts)
				_lastSkin.CheckTransparentPart(grabber, p.Key);

			_paintedPixels.Clear();
		}

		private void rendererControl_MouseDown(object sender, MouseEventArgs e)
		{
			Skin skin = _lastSkin;

			if (skin == null)
				return;

			CheckMouse(e.Y);

			float halfWidth = _rendererControl.Width / 2.0f;
			float halfImgWidth = 56.0f / 2.0f;

			var rect = new RectangleF(halfWidth - halfImgWidth, 0, halfImgWidth * 2, 22);

			_mousePoint = e.Location;

			if (rect.Contains(e.Location))
			{
				if (splitContainer4.SplitterDistance == 0)
					_opening = true;
				else
					_opening = false;

				_animTimer.Start();
				return;
			}

			_mouseIsDown = true;
			//_isValidPick = GetPick(e.X, e.Y, ref _pickPosition);

			if (e.Button == MouseButtons.Left)
			{
				if (_isValidPick)
					_selectedTool.Tool.BeginClick(_lastSkin, _pickPosition, e);
				else
					_selectedTool.Tool.BeginClick(_lastSkin, new Point(-1, -1), e);
				UseToolOnViewport(e.X, e.Y);
			}
			else
				_tools[(int) Tools.Camera].Tool.BeginClick(_lastSkin, Point.Empty, e);
		}

		private void rendererControl_MouseMove(object sender, MouseEventArgs e)
		{
			Skin skin = _lastSkin;

			if (skin == null)
				return;

			_isValidPick = GetPick(e.X, e.Y, ref _pickPosition);

			if (_mouseIsDown)
			{
				if (e.Button == MouseButtons.Left)
				{
					_selectedTool.Tool.MouseMove(_lastSkin, e);
					UseToolOnViewport(e.X, e.Y);
				}
				else
					_tools[(int) Tools.Camera].Tool.MouseMove(_lastSkin, e);

				_rendererControl.Invalidate();
			}

			_mousePoint = e.Location;
		}

		private void rendererControl_MouseUp(object sender, MouseEventArgs e)
		{
			Skin skin = _lastSkin;

			if (skin == null)
				return;

			if (_mouseIsDown)
			{
				var currentSkin = new ColorGrabber();

				if (e.Button == MouseButtons.Left)
				{
					currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);
					currentSkin.Load();

					if (_selectedTool.Tool.EndClick(ref currentSkin, skin, e))
					{
						SetCanSave(true);
						skin.Dirty = true;
						treeView1.Invalidate();
						currentSkin.Save();
					}

					SetPartTransparencies();
				}
				else
					_tools[(int) Tools.Camera].Tool.EndClick(ref currentSkin, _lastSkin, e);
			}

			_mouseIsDown = false;
			treeView1.Invalidate();
		}

		public void CheckUndo()
		{
			if (_currentUndoBuffer != null)
			{
				undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = _currentUndoBuffer.CanUndo;
				redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = _currentUndoBuffer.CanRedo;
			}
			else
			{
				undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = false;
				redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = false;
			}
		}

		private void rendererControl_MouseLeave(object sender, EventArgs e)
		{
			_mousePoint = new Point(-1, -1);
		}

		private void VerifySelectionButtons()
		{
			changeNameToolStripMenuItem.Enabled =
				deleteToolStripMenuItem.Enabled = cloneToolStripMenuItem.Enabled = cloneToolStripButton.Enabled = true;
			mDECRESToolStripMenuItem.Enabled = mINCRESToolStripMenuItem.Enabled = true;

			if (treeView1.SelectedNode == null)
			{
				mDECRESToolStripMenuItem.Enabled =
					mINCRESToolStripMenuItem.Enabled =
					changeNameToolStripMenuItem.Enabled =
					deleteToolStripMenuItem.Enabled =
					cloneToolStripMenuItem.Enabled =
					cloneToolStripButton.Enabled = false;
			}
			else if (!(treeView1.SelectedNode is Skin))
			{
				mDECRESToolStripMenuItem.Enabled =
					mINCRESToolStripMenuItem.Enabled =
					cloneToolStripMenuItem.Enabled =
					cloneToolStripButton.Enabled = false;
			}
			else if (treeView1.SelectedNode is Skin)
			{
				var skin = treeView1.SelectedNode as Skin;

				if (skin.Width <= 1 || skin.Height <= 1)
					mDECRESToolStripMenuItem.Enabled = false;
				//else if (skin.Width == 256 || skin.Height == 128)
				//	mINCRESToolStripMenuItem.Enabled = false;
			}

			string folderLocation = GetFolderLocationForNode(treeView1.SelectedNode);
			bool canDoOperation = string.IsNullOrEmpty(folderLocation);

			importToolStripButton.Enabled = !canDoOperation;
			newSkinToolStripButton.Enabled = !canDoOperation;
			newFolderToolStripButton.Enabled = !canDoOperation;
			fetchToolStripButton.Enabled = !canDoOperation;

			bool itemSelected = treeView1.SelectedNode == null;

			renameToolStripButton.Enabled = !itemSelected;
			deleteToolStripButton.Enabled = !itemSelected;
			decResToolStripButton.Enabled = !itemSelected;
			incResToolStripButton.Enabled = !itemSelected;
			uploadToolStripButton.Enabled = !itemSelected;
		}

		private Bitmap GenerateCheckBoxBitmap(CheckBoxState state)
		{
			var uncheckedImage = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			using (Graphics g = Graphics.FromImage(uncheckedImage))
			{
				Size glyphSize = CheckBoxRenderer.GetGlyphSize(g, state);
				CheckBoxRenderer.DrawCheckBox(g, new Point((16 / 2) - (glyphSize.Width / 2), (16 / 2) - (glyphSize.Height / 2)),
				                              state);
			}

			return uncheckedImage;
		}

		private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (treeView1.SelectedNode == _lastSkin ||
			    !(e.Node is Skin))
				return;

			_rendererControl.MakeCurrent();

			if (_lastSkin != null && treeView1.SelectedNode != _lastSkin)
			{
				// Copy over the current changes to the tex stored in the skin.
				// This allows us to pick up where we left off later, without undoing any work.
				_lastSkin.CommitChanges(GlobalDirtiness.CurrentSkin, false);
			}

			//if (_lastSkin != null)
			//	_lastSkin.Undo.Clear();

			var skin = (Skin) treeView1.SelectedNode;
			SetCanSave(skin.Dirty);

			if (skin == null)
			{
				_currentUndoBuffer = null;
				TextureGL.Unbind();

				var currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, 64, 32);
				currentSkin.Save();

				undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = false;
				redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = false;
			}
			else
			{
				var glImage = new ColorGrabber(skin.GLImage, skin.Width, skin.Height);
				glImage.Load();

				glImage.Texture = GlobalDirtiness.CurrentSkin;
				glImage.Save();
				glImage.Texture = _previewPaint;
				glImage.Save();

				_currentUndoBuffer = skin.Undo;
				CheckUndo();
			}

			_lastSkin = (Skin) treeView1.SelectedNode;

			SetModel(skin.Model);
			_rendererControl.Invalidate();

			VerifySelectionButtons();
			FillPartList();
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void animateToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleAnimation();
		}

		private void followCursorToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleFollowCursor();
		}

		private void grassToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleGrass();
		}

		private void treeView1_MouseUp(object sender, MouseEventArgs e)
		{
			treeView1_MouseDown(sender, e);
			if (e.Button == MouseButtons.Right)
				contextMenuStrip1.Show(Cursor.Position);
		}

		private void treeView1_MouseDown(object sender, MouseEventArgs e)
		{
			_rightClickedNode = treeView1.GetSelectedNodeAt(e.Location);
			VerifySelectionButtons();
		}

		private void undoToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			PerformUndo();
		}

		private void perspectiveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Perspective);
		}

		private void textureToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Orthographic);
		}

		private void perspectiveToolStripButton_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Perspective);
		}

		private void orthographicToolStripButton_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Orthographic);
		}

		private void hybridToolStripButton_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Hybrid);
		}

		private void hybridViewToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetViewMode(ViewMode.Hybrid);
		}

		private void offToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetTransparencyMode(TransparencyMode.Off);
		}

		private void helmetOnlyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetTransparencyMode(TransparencyMode.Helmet);
		}

		private void allToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetTransparencyMode(TransparencyMode.All);
		}

		private void headToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.Head);
		}

		private void helmetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.Helmet);
		}

		private void chestToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.Chest);
		}

		private void leftArmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.LeftArm);
		}

		private void rightArmToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.RightArm);
		}

		private void leftLegToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.LeftLeg);
		}

		private void rightLegToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.RightLeg);
		}

		private void toggleHeadToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.Head);
		}

		private void toggleHelmetToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.Helmet);
		}

		private void toggleChestToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.Chest);
		}

		private void toggleLeftArmToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.LeftArm);
		}

		private void toggleRightArmToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.RightArm);
		}

		private void toggleLeftLegToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.LeftLeg);
		}

		private void toggleRightLegToolStripButton_Click(object sender, EventArgs e)
		{
			ToggleVisiblePart(ModelPart.RightLeg);
		}

		private void alphaCheckerboardToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleAlphaCheckerboard();
		}

		private void textureOverlayToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleOverlay();
		}

		private void ShowUpdater()
		{
			MessageBox.Show("Not finished!");

			Hide();

			using (var upd = new UpdateSystem.Updater("http://alteredsoftworks.com/mcskin3d/updates.xml", "__installedupdates"))
			{
				if (upd.ShowDialog() == DialogResult.Cancel)
					Show();
				else
					Close();
			}
		}

		private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_updater.Checking)
			{
				_updater.Abort();
				ShowUpdater();
				return;
			}

			//_updater.PrintOnEqual = true;
			//_updater.CheckForUpdate();
			ShowUpdater();
		}

		private void undoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformUndo();
		}

		private void redoToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformRedo();
		}

		private void keyboardShortcutsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			_shortcutEditor.ShowDialog();
		}

		private void backgroundColorToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var picker = new ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.BackgroundColor;

				if (picker.ShowDialog() == DialogResult.OK)
				{
					GlobalSettings.BackgroundColor = picker.CurrentColor;

					_rendererControl.Invalidate();
				}
			}
		}

		private void screenshotToolStripButton_Click(object sender, EventArgs e)
		{
			if ((ModifierKeys & Keys.Shift) != 0)
				SaveScreenshot();
			else
				TakeScreenshot();
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformSaveAs();
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformSave();
		}

		private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformSaveAll();
		}

		private void saveToolStripButton_Click(object sender, EventArgs e)
		{
			PerformSave();
		}

		private void saveAlltoolStripButton_Click(object sender, EventArgs e)
		{
			PerformSaveAll();
		}

		private void changeNameToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformNameChange();
		}

		private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformDeleteSkin();
		}

		private void cloneToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformCloneSkin();
		}

		private void automaticallyCheckForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			GlobalSettings.AutoUpdate =
				automaticallyCheckForUpdatesToolStripMenuItem.Checked = !automaticallyCheckForUpdatesToolStripMenuItem.Checked;
		}

		private void labelEditTextBox_Leave(object sender, EventArgs e)
		{
			DoneEditingNode(labelEditTextBox.Text, _currentlyEditing);
		}

		private void labelEditTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Enter)
			{
				treeView1.Focus();
				e.Handled = true;
			}
		}

		private void labelEditTextBox_KeyPress(object sender, KeyPressEventArgs e)
		{
			if (e.KeyChar == '\r' || e.KeyChar == '\n')
				e.Handled = true;
		}

		private void labelEditTextBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Return)
				e.Handled = true;
		}

		private void importHereToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformImportSkin();
		}

		private void toolStripMenuItem1_Click(object sender, EventArgs e)
		{
			PerformNewFolder();
		}

		private void ghostHiddenPartsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ToggleGhosting();
		}

		private void SetSampleMenuItem(int samples)
		{
			if (_antialiasOpts == null)
			{
				_antialiasOpts = new[]
				                 {
				                 	xToolStripMenuItem4, xToolStripMenuItem, xToolStripMenuItem1, xToolStripMenuItem2,
				                 	xToolStripMenuItem3
				                 };
			}

			int index = 0;

			switch (samples)
			{
				case 0:
				default:
					index = 0;
					break;
				case 1:
					index = 1;
					break;
				case 2:
					index = 2;
					break;
				case 4:
					index = 3;
					break;
				case 8:
					index = 4;
					break;
			}

			foreach (ToolStripMenuItem item in _antialiasOpts)
				item.Checked = false;

			GlobalSettings.Multisamples = samples;
			_antialiasOpts[index].Checked = true;
		}

		private void xToolStripMenuItem4_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(0);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(1);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(2);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem2_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(4);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		private void xToolStripMenuItem3_Click(object sender, EventArgs e)
		{
			SetSampleMenuItem(8);
			MessageBox.Show(this, GetLanguageString("B_MSG_ANTIALIAS"));
		}

		public static string GetLanguageString(string id)
		{
			if (!_currentLanguage.StringTable.ContainsKey(id))
			{
#if BETA
				MessageBox.Show("Stringtable string not found: " + id);
#endif

				return id;
			}

			return _currentLanguage.StringTable[id];
		}

		private void MCSkin3D_Load(object sender, EventArgs e)
		{
			SwatchLoader.LoadSwatches();
		}

		private void languageToolStripMenuItem_Click(object sender, EventArgs e)
		{
			CurrentLanguage = (Language.Language) ((ToolStripMenuItem) sender).Tag;
		}

		private void toolStripMenuItem3_Click(object sender, EventArgs e)
		{
			PerformUpload();
		}

		private void importToolStripButton_Click(object sender, EventArgs e)
		{
			PerformImportSkin();
		}

		private void newFolderToolStripButton_Click(object sender, EventArgs e)
		{
			PerformNewFolder();
		}

		private void renameToolStripButton_Click(object sender, EventArgs e)
		{
			PerformNameChange();
		}

		private void deleteToolStripButton_Click(object sender, EventArgs e)
		{
			PerformDeleteSkin();
		}

		private void cloneToolStripButton_Click(object sender, EventArgs e)
		{
			PerformCloneSkin();
		}

		private void uploadToolStripButton_Click(object sender, EventArgs e)
		{
			PerformUpload();
		}

		private void toolStripButton2_Click(object sender, EventArgs e)
		{
			PerformTreeViewZoomOut();
		}

		private void toolStripButton1_Click(object sender, EventArgs e)
		{
			PerformTreeViewZoomIn();
		}

		private void hScrollBar1_Scroll(object sender, ScrollEventArgs e)
		{
			treeView1.ScrollPosition = new Point(hScrollBar1.Value, treeView1.ScrollPosition.Y);
		}

		private bool ShowDontAskAgain()
		{
			bool againValue = GlobalSettings.ResChangeDontShowAgain;
			bool ret = DontAskAgain.Show(CurrentLanguage, "M_IRREVERSIBLE", ref againValue);
			GlobalSettings.ResChangeDontShowAgain = againValue;

			return ret;
		}

		private void PerformDecreaseResolution()
		{
			if (_lastSkin == null)
				return;
			if (_lastSkin.Width <= 1 || _lastSkin.Height <= 1)
				return;
			if (!ShowDontAskAgain())
				return;

			_lastSkin.Resize(_lastSkin.Width / 2, _lastSkin.Height / 2);

			var grabber = new ColorGrabber(_lastSkin.GLImage, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();
			grabber.Texture = GlobalDirtiness.CurrentSkin;
			grabber.Save();
			grabber.Texture = _previewPaint;
			grabber.Save();
		}

		private void PerformIncreaseResolution()
		{
			if (_lastSkin == null)
				return;
			if (!ShowDontAskAgain())
				return;

			_lastSkin.Resize(_lastSkin.Width * 2, _lastSkin.Height * 2);

			var grabber = new ColorGrabber(_lastSkin.GLImage, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();
			grabber.Texture = GlobalDirtiness.CurrentSkin;
			grabber.Save();
			grabber.Texture = _previewPaint;
			grabber.Save();
		}

		public void PerformImportFromSite()
		{
			string accountName = _importFromSite.Show();

			if (string.IsNullOrEmpty(accountName))
				return;

			string url = "http://s3.amazonaws.com/MinecraftSkins/" + accountName + ".png";

			string folderLocation;
			TreeNodeCollection collection;

			if (_rightClickedNode == null)
				_rightClickedNode = treeView1.SelectedNode;

			GetFolderLocationAndCollectionForNode(treeView1, _rightClickedNode, out folderLocation, out collection);

			string newSkinName = accountName;

			while (File.Exists(folderLocation + newSkinName + ".png"))
				newSkinName += " - New";

			try
			{
				byte[] pngData = WebHelpers.DownloadFile(url);

				using (FileStream file = File.Create(folderLocation + newSkinName + ".png"))
					file.Write(pngData, 0, pngData.Length);

				var skin = new Skin(folderLocation + newSkinName + ".png");
				collection.Add(skin);
				skin.SetImages();

				treeView1.Invalidate();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, GetLanguageString("M_SKINERROR") + "\r\n" + ex);
				return;
			}
		}

		private void mDECRESToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformDecreaseResolution();
		}

		private void mINCRESToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformIncreaseResolution();
		}

		private void toolStripButton4_Click(object sender, EventArgs e)
		{
			PerformDecreaseResolution();
		}

		private void toolStripButton3_Click(object sender, EventArgs e)
		{
			PerformIncreaseResolution();
		}

		private void toolStripButton5_Click(object sender, EventArgs e)
		{
			PerformNewSkin();
		}

		private void toolStripButton6_Click(object sender, EventArgs e)
		{
			PerformImportFromSite();
		}

		private void mFETCHNAMEToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformImportFromSite();
		}

		private void toolStripMenuItem4_Click(object sender, EventArgs e)
		{
			PerformNewSkin();
		}

		private void statusStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
		}

		private void mSKINDIRSToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (_waitingForRestart)
			{
				MessageBox.Show(GetLanguageString("C_RESTART"), GetLanguageString("C_RESTART_CAPTION"), MessageBoxButtons.OK);
				return;
			}

			using (var dl = new DirectoryList())
			{
				dl.StartPosition = FormStartPosition.CenterParent;
				foreach (string dir in GlobalSettings.SkinDirectories.OrderBy(x => x))
					dl.Directories.Add(dir);

				if (dl.ShowDialog() == DialogResult.OK)
				{
					if (RecursiveNodeIsDirty(treeView1.Nodes))
					{
						DialogResult mb = MessageBox.Show(GetLanguageString("D_UNSAVED"), GetLanguageString("D_UNSAVED_CAPTION"),
						                                  MessageBoxButtons.YesNo, MessageBoxIcon.Question);
						switch (mb)
						{
							case DialogResult.No:
								_waitingForRestart = true;
								_newSkinDirs = dl.Directories.ToArray();
								return;
							case DialogResult.Cancel:
								return;
						}
					}

					GlobalSettings.SkinDirectories = dl.Directories.ToArray();
				}
			}
		}

		private void modeToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			Perform10Mode();
		}

		private void mINVERTBOTTOMToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (treeView1.SelectedNode == null ||
			    _lastSkin == null)
				return;

			var grabber = new ColorGrabber(GlobalDirtiness.CurrentSkin, _lastSkin.Width, _lastSkin.Height);
			grabber.Load();

			var toInvert = new List<Rectangle>();

			foreach (Mesh meshes in CurrentModel.Meshes)
			{
				foreach (Face face in meshes.Faces)
				{
					if (face.Downface)
					{
						Rectangle rect = face.TexCoordsToInteger(_lastSkin.Width, _lastSkin.Height);

						if (!toInvert.Contains(rect))
							toInvert.Add(rect);
					}
				}
			}

			var undoable = new PixelsChangedUndoable(GetLanguageString("U_PIXELSCHANGED"), GetLanguageString("M_INVERTBOTTOM"));

			foreach (Rectangle rect in toInvert)
			{
				for (int x = rect.X; x < rect.X + rect.Width; ++x)
				{
					for (int y = rect.Y, y2 = rect.Y + rect.Height - 1; y2 > y; ++y, --y2)
					{
						ColorPixel topPixel = grabber[x, y];
						ColorPixel bottomPixel = grabber[x, y2];

						undoable.Points.Add(new Point(x, y),
						                    Tuple.MakeTuple(Color.FromArgb(topPixel.Alpha, topPixel.Red, topPixel.Green, topPixel.Blue),
						                                    new ColorAlpha(
						                                    	Color.FromArgb(bottomPixel.Alpha, bottomPixel.Red, bottomPixel.Green,
						                                    	               bottomPixel.Blue), -1)));
						undoable.Points.Add(new Point(x, y2),
						                    Tuple.MakeTuple(
						                    	Color.FromArgb(bottomPixel.Alpha, bottomPixel.Red, bottomPixel.Green, bottomPixel.Blue),
						                    	new ColorAlpha(Color.FromArgb(topPixel.Alpha, topPixel.Red, topPixel.Green, topPixel.Blue),
						                    	               -1)));

						grabber[x, y] = bottomPixel;
						grabber[x, y2] = topPixel;
					}
				}
			}

			_lastSkin.Undo.AddBuffer(undoable);
			CheckUndo();
			SetCanSave(_lastSkin.Dirty = true);

			grabber.Save();
		}

		private void mLINESIZEToolStripMenuItem_NumericBox_ValueChanged(object sender, EventArgs e)
		{
			GlobalSettings.DynamicOverlayLineSize = (int) mLINESIZEToolStripMenuItem.NumericBox.Value;
		}

		private void mOVERLAYTEXTSIZEToolStripMenuItem_NumericBox_ValueChanged(object sender, EventArgs e)
		{
			GlobalSettings.DynamicOverlayTextSize = (int) mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Value;
		}

		private void mGRIDOPACITYToolStripMenuItem_NumericBox_ValueChanged(object sender, EventArgs e)
		{
			GlobalSettings.DynamicOverlayGridColor = Color.FromArgb((int) mGRIDOPACITYToolStripMenuItem.NumericBox.Value,
			                                                        GlobalSettings.DynamicOverlayGridColor);
		}

		public void SetModel(Model Model)
		{
			if (_lastSkin == null)
				return;

			if (_oldModel != null &&
			    _oldModel.Model == Model)
				return;

			if (_lastSkin.Model != Model)
			{
				_lastSkin.Model = Model;

				_lastSkin.Dirty = true;
				SetCanSave(true);
				CheckUndo();
			}

			if (_oldModel != null)
			{
				_oldModel.Checked = false;

				for (ToolStripItem parent = _oldModel.OwnerItem; parent != null; parent = parent.OwnerItem)
					parent.Image = null;
			}

			toolStripDropDownButton1.Text = _lastSkin.Model.Name;
			_oldModel = _lastSkin.Model.DropDownItem;
			_oldModel.Checked = true;

			_lastSkin.TransparentParts.Clear();
			_lastSkin.SetTransparentParts();
			FillPartList();

			for (ToolStripItem parent = _oldModel.OwnerItem; parent != null; parent = parent.OwnerItem)
				parent.Image = Resources.right_arrow_next;

			CalculateMatrices();
			_rendererControl.Invalidate();
		}

		private void resetCameraToolStripButton_Click(object sender, EventArgs e)
		{
			PerformResetCamera();
		}

		private void mLINECOLORToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var picker = new ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.DynamicOverlayLineColor;

				if (picker.ShowDialog() == DialogResult.OK)
				{
					GlobalSettings.DynamicOverlayLineColor = picker.CurrentColor;
					mLINECOLORToolStripMenuItem.BackColor = GlobalSettings.DynamicOverlayLineColor;

					_rendererControl.Invalidate();
				}
			}
		}

		private void mTEXTCOLORToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var picker = new ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.DynamicOverlayTextColor;

				if (picker.ShowDialog() == DialogResult.OK)
				{
					GlobalSettings.DynamicOverlayTextColor = picker.CurrentColor;
					mTEXTCOLORToolStripMenuItem.BackColor = GlobalSettings.DynamicOverlayTextColor;

					_rendererControl.Invalidate();
				}
			}
		}

		private void mGRIDCOLORToolStripMenuItem_Click(object sender, EventArgs e)
		{
			using (var picker = new ColorPicker())
			{
				picker.CurrentColor = GlobalSettings.DynamicOverlayGridColor;

				if (picker.ShowDialog() == DialogResult.OK)
				{
					GlobalSettings.DynamicOverlayGridColor = picker.CurrentColor;
					mGRIDCOLORToolStripMenuItem.BackColor = Color.FromArgb(255, GlobalSettings.DynamicOverlayGridColor);

					_rendererControl.Invalidate();
				}
			}
		}

		private void mINFINITEMOUSEToolStripMenuItem_Click(object sender, EventArgs e)
		{
			mINFINITEMOUSEToolStripMenuItem.Checked = !mINFINITEMOUSEToolStripMenuItem.Checked;
			GlobalSettings.InfiniteMouse = mINFINITEMOUSEToolStripMenuItem.Checked;
		}

		private void PerformBrowseTo()
		{
			if (treeView1.SelectedNode is Skin)
				Process.Start("explorer.exe", "/select,\"" + ((Skin) treeView1.SelectedNode).File.FullName + "\"");
			else
				Process.Start("explorer.exe", ((FolderNode) treeView1.SelectedNode).Directory.FullName);
		}

		private void bROWSEIDToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PerformBrowseTo();
		}

		private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
		{
			bROWSEIDToolStripMenuItem.Text = string.Format(GetLanguageString("M_BROWSE"),
			                                               (treeView1.SelectedNode is Skin)
			                                               	? GetLanguageString("M_SKIN")
			                                               	: GetLanguageString("M_FOLDER"));
		}

		private void mRENDERSTATSToolStripMenuItem_Click(object sender, EventArgs e)
		{
			mRENDERSTATSToolStripMenuItem.Checked = !mRENDERSTATSToolStripMenuItem.Checked;
			GlobalSettings.RenderBenchmark = mRENDERSTATSToolStripMenuItem.Checked;
		}

		private void toolStripButton7_Click(object sender, EventArgs e)
		{
			splitContainer1.Panel2Collapsed = !splitContainer1.Panel2Collapsed;

			if (_popoutForm == null)
			{
				int oldWidth = Width;
				Width = helpToolStripMenuItem.Bounds.X + helpToolStripMenuItem.Bounds.Width + 18;

				// Move the items to a new form
				_popoutForm = new Form();
				_popoutForm.Height = Height;
				_popoutForm.Width = (oldWidth - Width) + 4;
				_popoutForm.Text = "Render Window";
				_popoutForm.Icon = Icon;
				_popoutForm.ControlBox = false;
				_popoutForm.Show();
				_popoutForm.Location = new Point(Location.X + Width, Location.Y);
				_popoutForm.KeyDown += popoutForm_KeyDown;
				_popoutForm.KeyPreview = true;

				splitContainer1.Panel2.Controls.Remove(panel4);
				_popoutForm.Controls.Add(panel4);
			}
			else
			{
				_popoutForm.Controls.Remove(panel4);
				splitContainer1.Panel2.Controls.Add(panel4);

				Width += _popoutForm.Width - 4;

				_popoutForm.Close();
				_popoutForm.Dispose();
				_popoutForm = null;
			}
		}

		private void CreatePartList()
		{
			var list = new ImageList();
			list.ColorDepth = ColorDepth.Depth32Bit;
			list.ImageSize = new Size(16, 16);
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.UncheckedNormal));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.UncheckedHot));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.UncheckedPressed));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.CheckedNormal));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.CheckedHot));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.CheckedPressed));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.MixedNormal));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.MixedHot));
			list.Images.Add(GenerateCheckBoxBitmap(CheckBoxState.MixedPressed));

			treeView2.ImageList = list;
		}

		private PartTreeNode CreateNodePath(TreeView treeView, Model m, Mesh part, string[] path, List<PartTreeNode> owners)
		{
			PartTreeNode node = null;

			for (int i = 0; i < path.Length - 1; ++i)
			{
				if (node == null)
				{
					TreeNode[] nodes = treeView.Nodes.Find(path[i], false);

					if (nodes.Length == 0)
					{
						treeView.Nodes.Add(node = new PartTreeNode(CurrentModel, part, -1, path[i]));
						owners.Add(node);
					}
					else
						node = (PartTreeNode) nodes[0];
				}
				else
				{
					TreeNode[] nodes = node.Nodes.Find(path[i], false);

					if (nodes.Length == 0)
					{
						PartTreeNode old = node;
						old.Nodes.Add(node = new PartTreeNode(CurrentModel, part, -1, path[i]));
						owners.Add(node);
					}
					else
						node = (PartTreeNode) nodes[0];
				}
			}

			return node;
		}

		private void FillPartList()
		{
			var owners = new List<PartTreeNode>();

			treeView2.Nodes.Clear();

			int meshIndex = 0;
			foreach (Mesh part in CurrentModel.Meshes)
			{
				string name = part.Name;

				if (name.Contains('.'))
				{
					string[] args = name.Split('.');
					PartTreeNode owner = CreateNodePath(treeView2, CurrentModel, part, args, owners);

					owner.Nodes.Add(new PartTreeNode(CurrentModel, part, meshIndex, args[args.Length - 1]));
				}
				else
					treeView2.Nodes.Add(new PartTreeNode(CurrentModel, part, meshIndex));

				meshIndex++;
			}

			foreach (PartTreeNode n in owners)
				n.CheckGroupImage();

			for (int i = 1; i <= (int)ModelPart.RightLeg; ++i)
				CheckQuickPartState((ModelPart)i);
		}

		private void RecursiveAssign(PartTreeNode node, bool setAll)
		{
			foreach (PartTreeNode subNode in node.Nodes)
			{
				if (subNode.Nodes.Count != 0)
					RecursiveAssign(subNode, setAll);
				else
				{
					if (setAll)
						subNode.PartEnabled = true;
					else
						subNode.PartEnabled = !subNode.PartEnabled;
				}
			}
		}

		void CheckQuickPartState(ModelPart part)
		{
			var meshes = new List<Mesh>();
			var item = _partItems[(int)part];
			var button = _partButtons[(int)part];

			foreach (var m in CurrentModel.Meshes)
				if (m.Part == part)
					meshes.Add(m);

			if (meshes.Count == 0)
			{
				item.Enabled = button.Enabled = false;
				return;
			}

			item.Enabled = button.Enabled = true;
			item.Checked = button.Checked = true;

			foreach (var m in meshes)
			{
				if (CurrentModel.PartsEnabled[CurrentModel.Meshes.IndexOf(m)])
					continue;

				item.Checked = button.Checked = false;
				break;
			}
		}

		private void treeView2_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			var nodeBounds = e.Node.Bounds;
			var pos = e.Location;
			var node = (PartTreeNode) e.Node;

			if (pos.X > nodeBounds.X - 18 && pos.X < nodeBounds.X - 4)
			{
				if (node.Nodes.Count != 0)
				{
					bool setAll = node.ImageIndex == 6;
					RecursiveAssign(node, setAll);
				}
				else
					node.PartEnabled = !node.PartEnabled;

				for (int i = 1; i <= (int)ModelPart.RightLeg; ++i)
					CheckQuickPartState((ModelPart)i);

				CalculateMatrices();
				_rendererControl.Invalidate();
			}
		}

		#region Update

		public void Invoke(Action action)
		{
			Invoke((Delegate) action);
		}

		private void _updater_SameVersion(object sender, EventArgs e)
		{
			Invoke(() => MessageBox.Show(this, GetLanguageString("B_MSG_UPTODATE")));
		}

		private void _updater_NewVersionAvailable(object sender, EventArgs e)
		{
			Invoke(delegate
			       {
			       	if (MessageBox.Show(this, GetLanguageString("B_MSG_NEWUPDATE"), "Woo!", MessageBoxButtons.YesNo) ==
			       	    DialogResult.Yes)
			       		ShowUpdater();
			       });
		}

		#endregion

		#region Shortcuts

		private string CompileShortcutKeys()
		{
			string c = "";

			for (int i = 0; i < _shortcutEditor.ShortcutCount; ++i)
			{
				IShortcutImplementor shortcut = _shortcutEditor.ShortcutAt(i);

				if (i != 0)
					c += "|";

				Keys key = shortcut.Keys & ~Keys.Modifiers;
				var modifiers = (Keys) ((int) shortcut.Keys - (int) key);

				if (modifiers != 0)
					c += shortcut.SaveName + "=" + key + "+" + modifiers;
				else
					c += shortcut.SaveName + "=" + key;
			}

			return c;
		}

		private IShortcutImplementor FindShortcut(string name)
		{
			foreach (IShortcutImplementor s in _shortcutEditor.Shortcuts)
			{
				if (s.SaveName == name)
					return s;
			}

			return null;
		}

		private void LoadShortcutKeys(string s)
		{
			if (string.IsNullOrEmpty(s))
				return; // leave defaults

			string[] shortcuts = s.Split('|');

			foreach (string shortcut in shortcuts)
			{
				string[] args = shortcut.Split('=');

				string name = args[0];
				string key;
				string modifiers = "0";

				if (args[1].Contains('+'))
				{
					string[] mods = args[1].Split('+');

					key = mods[0];
					modifiers = mods[1];
				}
				else
					key = args[1];

				IShortcutImplementor sh = FindShortcut(name);

				if (sh == null)
					continue;

				sh.Keys = (Keys) Enum.Parse(typeof (Keys), key) | (Keys) Enum.Parse(typeof (Keys), modifiers);
			}
		}

		private void InitMenuShortcut(ToolStripMenuItem item, Action callback)
		{
			var shortcut = new MenuStripShortcut(item);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		private void InitMenuShortcut(ToolStripMenuItem item, Keys keys, Action callback)
		{
			var shortcut = new MenuStripShortcut(item, keys);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		private void InitUnlinkedShortcut(string name, Keys defaultKeys, Action callback)
		{
			var shortcut = new ShortcutBase(name, defaultKeys);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		private void InitControlShortcut(string name, Control control, Keys defaultKeys, Action callback)
		{
			var shortcut = new ControlShortcut(name, defaultKeys, control);
			shortcut.Pressed = callback;

			_shortcutEditor.AddShortcut(shortcut);
		}

		private void InitShortcuts()
		{
			// shortcut menus
			InitMenuShortcut(undoToolStripMenuItem, PerformUndo);
			InitMenuShortcut(redoToolStripMenuItem, PerformRedo);
			InitMenuShortcut(perspectiveToolStripMenuItem, () => SetViewMode(ViewMode.Perspective));
			InitMenuShortcut(textureToolStripMenuItem, () => SetViewMode(ViewMode.Orthographic));
			InitMenuShortcut(hybridViewToolStripMenuItem, () => SetViewMode(ViewMode.Hybrid));
			InitMenuShortcut(animateToolStripMenuItem, ToggleAnimation);
			InitMenuShortcut(followCursorToolStripMenuItem, ToggleFollowCursor);
			InitMenuShortcut(grassToolStripMenuItem, ToggleGrass);
			InitMenuShortcut(ghostHiddenPartsToolStripMenuItem, ToggleGhosting);
			InitMenuShortcut(alphaCheckerboardToolStripMenuItem, ToggleAlphaCheckerboard);
			InitMenuShortcut(textureOverlayToolStripMenuItem, ToggleOverlay);
			InitMenuShortcut(offToolStripMenuItem, () => SetTransparencyMode(TransparencyMode.Off));
			InitMenuShortcut(helmetOnlyToolStripMenuItem, () => SetTransparencyMode(TransparencyMode.Helmet));
			InitMenuShortcut(allToolStripMenuItem, () => SetTransparencyMode(TransparencyMode.All));
			InitMenuShortcut(headToolStripMenuItem,
			                 () => ToggleVisiblePart(ModelPart.Head));
			InitMenuShortcut(helmetToolStripMenuItem,
			                 () => ToggleVisiblePart(ModelPart.Helmet));
			InitMenuShortcut(chestToolStripMenuItem,
			                 () => ToggleVisiblePart(ModelPart.Chest));
			InitMenuShortcut(leftArmToolStripMenuItem,
			                 () => ToggleVisiblePart(ModelPart.LeftArm));
			InitMenuShortcut(rightArmToolStripMenuItem,
			                 () =>
			                 ToggleVisiblePart(ModelPart.RightArm));
			InitMenuShortcut(leftLegToolStripMenuItem,
			                 () => ToggleVisiblePart(ModelPart.LeftLeg));
			InitMenuShortcut(rightLegToolStripMenuItem,
			                 () =>
			                 ToggleVisiblePart(ModelPart.RightLeg));
			InitMenuShortcut(saveToolStripMenuItem, PerformSave);
			InitMenuShortcut(saveAsToolStripMenuItem, PerformSaveAs);
			InitMenuShortcut(saveAllToolStripMenuItem, PerformSaveAll);
			InitMenuShortcut(uploadToolStripMenuItem, PerformUpload);

			foreach (ToolIndex item in _tools)
				InitMenuShortcut(item.MenuItem, item.DefaultKeys, item.SetMeAsTool);

			// not in the menu
			InitUnlinkedShortcut("S_TOGGLETRANS", Keys.Shift | Keys.U, ToggleTransparencyMode);
			InitUnlinkedShortcut("S_TOGGLEVIEW", Keys.Control | Keys.V, ToggleViewMode);
			InitUnlinkedShortcut("S_SCREENSHOT_CLIP", Keys.Control | Keys.B, TakeScreenshot);
			InitUnlinkedShortcut("S_SCREENSHOT_SAVE", Keys.Control | Keys.Shift | Keys.B, SaveScreenshot);
			InitUnlinkedShortcut("S_DELETE", Keys.Delete, PerformDeleteSkin);
			InitUnlinkedShortcut("S_CLONE", Keys.Control | Keys.C, PerformCloneSkin);
			InitUnlinkedShortcut("S_RENAME", Keys.Control | Keys.R, PerformNameChange);
			InitUnlinkedShortcut("T_TREE_IMPORTHERE", Keys.Control | Keys.I, PerformImportSkin);
			InitUnlinkedShortcut("T_TREE_NEWFOLDER", Keys.Control | Keys.Shift | Keys.N, PerformNewFolder);
			InitUnlinkedShortcut("M_NEWSKIN_HERE", Keys.Control | Keys.Shift | Keys.M, PerformNewSkin);
			InitUnlinkedShortcut("S_COLORSWAP", Keys.S, PerformSwitchColor);
			InitControlShortcut("S_SWATCH_ZOOMIN", ColorPanel.SwatchContainer.SwatchDisplayer, Keys.Oemplus, PerformSwatchZoomIn);
			InitControlShortcut("S_SWATCH_ZOOMOUT", ColorPanel.SwatchContainer.SwatchDisplayer, Keys.OemMinus,
			                    PerformSwatchZoomOut);
			InitControlShortcut("S_TREEVIEW_ZOOMIN", treeView1, Keys.Control | Keys.Oemplus, PerformTreeViewZoomIn);
			InitControlShortcut("S_TREEVIEW_ZOOMOUT", treeView1, Keys.Control | Keys.OemMinus, PerformTreeViewZoomOut);
			InitUnlinkedShortcut("T_DECRES", Keys.Control | Keys.Shift | Keys.D, PerformDecreaseResolution);
			InitUnlinkedShortcut("T_INCRES", Keys.Control | Keys.Shift | Keys.I, PerformIncreaseResolution);
			InitUnlinkedShortcut("T_RESETCAMERA", Keys.Control | Keys.Shift | Keys.R, PerformResetCamera);
		}

		private void PerformSwitchColor()
		{
			colorPanel.SwitchColors();
		}

		public void SetSelectedTool(ToolIndex index)
		{
			if (_selectedTool != null)
				_selectedTool.MenuItem.Checked = _selectedTool.Button.Checked = false;

			ToolIndex oldTool = _selectedTool;
			_selectedTool = index;
			index.MenuItem.Checked = index.Button.Checked = true;

			splitContainer4.Panel1.Controls.Clear();

			if (oldTool != null && oldTool.OptionsPanel != null)
				oldTool.OptionsPanel.BoxHidden();
			if (_selectedTool.OptionsPanel != null)
				_selectedTool.OptionsPanel.BoxShown();

			if (_selectedTool.OptionsPanel != null)
			{
				_selectedTool.OptionsPanel.Dock = DockStyle.Fill;
				splitContainer4.Panel1.Controls.Add(_selectedTool.OptionsPanel);
			}

			toolStripStatusLabel1.Text = index.Tool.GetStatusLabelText();
		}

		private void ToolMenuItemClicked(object sender, EventArgs e)
		{
			var item = (ToolStripItem) sender;
			SetSelectedTool((ToolIndex) item.Tag);
		}

		private void PerformTreeViewZoomIn()
		{
			treeView1.ZoomIn();
		}

		private void PerformTreeViewZoomOut()
		{
			treeView1.ZoomOut();
		}

		private void PerformSwatchZoomOut()
		{
			colorPanel.SwatchContainer.ZoomOut();
		}

		private void PerformSwatchZoomIn()
		{
			colorPanel.SwatchContainer.ZoomIn();
		}

		public static bool PerformShortcut(Keys key, Keys modifiers)
		{
			foreach (IShortcutImplementor shortcut in _shortcutEditor.Shortcuts)
			{
				if (shortcut.CanEvaluate() && (shortcut.Keys & ~Keys.Modifiers) == key &&
				    (shortcut.Keys & ~(shortcut.Keys & ~Keys.Modifiers)) == modifiers)
				{
					shortcut.Pressed();
					return true;
				}
			}

			return false;
		}

		#endregion

		#region Overrides

		private TreeNode _tempToSelect;

		private bool CheckKeyShortcut(KeyEventArgs e)
		{
			if (!colorPanel.HexTextBox.ContainsFocus &&
			    !labelEditTextBox.ContainsFocus &&
			    !colorPanel.SwatchContainer.SwatchRenameTextBoxHasFocus)
			{
				if (PerformShortcut(e.KeyCode & ~Keys.Modifiers, e.Modifiers))
					return true;
			}

			return false;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if ((e.Handled = CheckKeyShortcut(e)))
				return;

			base.OnKeyDown(e);
		}

		private void popoutForm_KeyDown(object sender, KeyEventArgs e)
		{
			e.Handled = CheckKeyShortcut(e);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			SwatchLoader.CancelLoadSwatches();

			if (RecursiveNodeIsDirty(treeView1.Nodes))
			{
				if (
					MessageBox.Show(this, GetLanguageString("C_UNSAVED"), GetLanguageString("C_UNSAVED_CAPTION"),
					                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
				{
					e.Cancel = true;
					return;
				}
			}

			_updater.Abort();
			GlobalSettings.ShortcutKeys = CompileShortcutKeys();

			colorPanel.SwatchContainer.SaveSwatches();

			if (_newSkinDirs != null)
				GlobalSettings.SkinDirectories = _newSkinDirs;

			GlobalSettings.Save();
		}

		private void RecurseAddDirectories(string path, TreeNodeCollection nodes, List<Skin> skins)
		{
			var di = new DirectoryInfo(path);

			foreach (FileInfo file in di.GetFiles("*.png", SearchOption.TopDirectoryOnly))
			{
				var skin = new Skin(file);
				nodes.Add(skin);

				if (_tempToSelect == null)
					_tempToSelect = skin;
				else if (GlobalSettings.LastSkin == skin.File.ToString())
					_tempToSelect = skin;

				skins.Add(skin);
			}

			foreach (DirectoryInfo dir in di.GetDirectories())
			{
				if ((dir.Attributes & FileAttributes.Hidden) != 0)
					continue;

				var folderNode = new FolderNode(dir.FullName);
				RecurseAddDirectories(dir.FullName, folderNode.Nodes, skins);
				nodes.Add(folderNode);
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			SetTransparencyMode(GlobalSettings.Transparency);
			SetViewMode(_currentViewMode);

			_rendererControl.MakeCurrent();

			var skins = new List<Skin>();
			treeView1.BeginUpdate();

			if (HasOneRoot)
				RecurseAddDirectories(RootFolderString, treeView1.Nodes, skins);
			else
			{
				foreach (string x in GlobalSettings.SkinDirectories)
				{
					var folder = new FolderNode(x);
					RecurseAddDirectories(x, folder.Nodes, skins);
					treeView1.Nodes.Add(folder);
				}
			}

			foreach (Skin s in skins)
				s.SetImages();

			treeView1.EndUpdate();
			treeView1.SelectedNode = _tempToSelect;

			toolToolStripMenuItem.DropDown.Closing += DontCloseMe;
			modeToolStripMenuItem.DropDown.Closing += DontCloseMe;
			threeDToolStripMenuItem.DropDown.Closing += DontCloseMe;
			twoDToolStripMenuItem.DropDown.Closing += DontCloseMe;
			transparencyModeToolStripMenuItem.DropDown.Closing += DontCloseMe;
			visiblePartsToolStripMenuItem.DropDown.Closing += DontCloseMe;
			mSHAREDToolStripMenuItem.DropDown.Closing += DontCloseMe;

			optionsToolStripMenuItem.DropDown.Closing += (sender, args) =>
			                                             {
			                                             	if (modeToolStripMenuItem1.Selected &&
			                                             	    (args.CloseReason == ToolStripDropDownCloseReason.ItemClicked ||
			                                             	     args.CloseReason == ToolStripDropDownCloseReason.Keyboard))
			                                             		args.Cancel = true;
			                                             };

			if (Screen.PrimaryScreen.BitsPerPixel != 32)
			{
				MessageBox.Show(GetLanguageString("B_MSG_PIXELFORMAT"), GetLanguageString("B_CAP_SORRY"), MessageBoxButtons.OK,
				                MessageBoxIcon.Error);
				Application.Exit();
			}

			if (Environment.CurrentDirectory.StartsWith(Environment.ExpandEnvironmentVariables("%temp%")))
			{
				MessageBox.Show(GetLanguageString("M_TEMP"));
				Application.Exit();
			}

			//new GUIDPicker("..\\update_guids.txt").ShowDialog();
		}

		private void DontCloseMe(object sender, ToolStripDropDownClosingEventArgs e)
		{
			if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked ||
			    e.CloseReason == ToolStripDropDownCloseReason.Keyboard)
				e.Cancel = true;
		}

		#endregion

		#region Private Functions

		private readonly Dictionary<Point, bool> _paintedPixels = new Dictionary<Point, bool>();

		public Dictionary<Point, bool> PaintedPixels
		{
			get { return _paintedPixels; }
		}

		private void PixelWritten(Point p, ColorPixel c)
		{
			if (!_paintedPixels.ContainsKey(p))
				_paintedPixels.Add(p, true);
		}

		private void UseToolOnViewport(int x, int y, bool begin = false)
		{
			if (_lastSkin == null)
				return;

			if (_isValidPick)
			{
				Skin skin = _lastSkin;

				var currentSkin = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);
				currentSkin.Load();

				currentSkin.OnWrite = PixelWritten;

				if (_selectedTool.Tool.MouseMoveOnSkin(ref currentSkin, skin, _pickPosition.X, _pickPosition.Y))
				{
					SetCanSave(true);
					skin.Dirty = true;
					currentSkin.Save();
				}
			}

			_rendererControl.Invalidate();
		}

		private void ToggleAnimation()
		{
			return;

			animateToolStripMenuItem.Checked = !animateToolStripMenuItem.Checked;
			GlobalSettings.Animate = animateToolStripMenuItem.Checked;

			_rendererControl.Invalidate();
		}

		private void ToggleFollowCursor()
		{
			return;

			followCursorToolStripMenuItem.Checked = !followCursorToolStripMenuItem.Checked;
			GlobalSettings.FollowCursor = followCursorToolStripMenuItem.Checked;

			_rendererControl.Invalidate();
		}

		private void ToggleGrass()
		{
			grassToolStripMenuItem.Checked = !grassToolStripMenuItem.Checked;
			GlobalSettings.Grass = grassToolStripMenuItem.Checked;

			_rendererControl.Invalidate();
		}

		private void ToggleGhosting()
		{
			ghostHiddenPartsToolStripMenuItem.Checked = !ghostHiddenPartsToolStripMenuItem.Checked;
			GlobalSettings.Ghost = ghostHiddenPartsToolStripMenuItem.Checked;

			_rendererControl.Invalidate();
		}

		private void Perform10Mode()
		{
			ModelLoader.InvertBottomFaces();

			modeToolStripMenuItem1.Checked = !modeToolStripMenuItem1.Checked;
			GlobalSettings.OnePointEightMode = modeToolStripMenuItem1.Checked;

			_rendererControl.Invalidate();
		}

		private void DoneEditingNode(string newName, TreeNode _currentlyEditing)
		{
			labelEditTextBox.Hide();

			if (_currentlyEditing is Skin)
			{
				var skin = (Skin) _currentlyEditing;

				if (skin.Name == newName)
					return;

				if (skin.ChangeName(newName) == false)
					SystemSounds.Beep.Play();
			}
			else
			{
				var folder = (FolderNode) _currentlyEditing;

				folder.MoveTo(((_currentlyEditing.Parent != null)
				               	? (GetFolderForNode(_currentlyEditing.Parent) + '\\' + newName)
				               	: newName));
			}
		}

		private void BeginUndo()
		{
			_rendererControl.MakeCurrent();
		}

		private void DoUndo()
		{
			if (!_currentUndoBuffer.CanUndo)
				throw new Exception();

			_currentUndoBuffer.Undo();
		}

		private void EndUndo()
		{
			undoToolStripMenuItem.Enabled = undoToolStripButton.Enabled = _currentUndoBuffer.CanUndo;
			redoToolStripMenuItem.Enabled = redoToolStripButton.Enabled = _currentUndoBuffer.CanRedo;

			SetCanSave(_lastSkin.Dirty = true);

			_rendererControl.Invalidate();
		}

		private void PerformUndo()
		{
			if (!_currentUndoBuffer.CanUndo)
				return;

			BeginUndo();
			DoUndo();
			EndUndo();
		}

		private void UndoListBox_MouseClick(object sender, MouseEventArgs e)
		{
			undoToolStripButton.DropDown.Close();

			if (!_currentUndoBuffer.CanUndo)
				return;

			BeginUndo();
			for (int i = 0; i <= _undoListBox.ListBox.HighestItemSelected; ++i)
				DoUndo();
			EndUndo();
		}

		private void undoToolStripButton_DropDownOpening(object sender, EventArgs e)
		{
			_undoListBox.ListBox.Items.Clear();

			foreach (IUndoable x in _currentUndoBuffer.UndoList)
				_undoListBox.ListBox.Items.Insert(0, x.Action);
		}

		private void BeginRedo()
		{
			if (!_currentUndoBuffer.CanRedo)
				return;

			_rendererControl.MakeCurrent();
		}

		private void DoRedo()
		{
			_currentUndoBuffer.Redo();
		}

		private void EndRedo()
		{
			SetCanSave(_lastSkin.Dirty = true);

			_rendererControl.Invalidate();
		}

		private void PerformRedo()
		{
			BeginRedo();
			DoRedo();
			EndRedo();
		}

		private void RedoListBox_MouseClick(object sender, MouseEventArgs e)
		{
			redoToolStripButton.DropDown.Close();

			BeginRedo();
			for (int i = 0; i <= _redoListBox.ListBox.HighestItemSelected; ++i)
				DoRedo();
			EndRedo();
		}

		private void redoToolStripButton_ButtonClick(object sender, EventArgs e)
		{
			PerformRedo();
		}

		private void redoToolStripButton_DropDownOpening(object sender, EventArgs e)
		{
			_redoListBox.ListBox.Items.Clear();

			foreach (IUndoable x in _currentUndoBuffer.RedoList)
				_redoListBox.ListBox.Items.Insert(0, x.Action);
		}

		private void SetViewMode(ViewMode newMode)
		{
			perspectiveToolStripButton.Checked = orthographicToolStripButton.Checked = hybridToolStripButton.Checked = false;
			perspectiveToolStripMenuItem.Checked = textureToolStripMenuItem.Checked = hybridViewToolStripMenuItem.Checked = false;
			_currentViewMode = newMode;

			switch (_currentViewMode)
			{
				case ViewMode.Orthographic:
					orthographicToolStripButton.Checked = true;
					textureToolStripMenuItem.Checked = true;
					break;
				case ViewMode.Perspective:
					perspectiveToolStripButton.Checked = true;
					perspectiveToolStripMenuItem.Checked = true;
					break;
				case ViewMode.Hybrid:
					hybridToolStripButton.Checked = true;
					hybridViewToolStripMenuItem.Checked = true;
					break;
			}

			CalculateMatrices();
			_rendererControl.Invalidate();
		}

		private void SetTransparencyMode(TransparencyMode trans)
		{
			offToolStripMenuItem.Checked = helmetOnlyToolStripMenuItem.Checked = allToolStripMenuItem.Checked = false;
			GlobalSettings.Transparency = trans;

			switch (GlobalSettings.Transparency)
			{
				case TransparencyMode.Off:
					offToolStripMenuItem.Checked = true;
					break;
				case TransparencyMode.Helmet:
					helmetOnlyToolStripMenuItem.Checked = true;
					break;
				case TransparencyMode.All:
					allToolStripMenuItem.Checked = true;
					break;
			}

			_rendererControl.Invalidate();
		}

		private void ToggleVisiblePart(ModelPart part)
		{
			var meshes = new List<Mesh>();
			var item = _partItems[(int)part];
			var button = _partButtons[(int)part];

			foreach (var m in CurrentModel.Meshes)
				if (m.Part == part)
					meshes.Add(m);

			if (meshes.Count != 0)
			{
				item.Checked = button.Checked = !item.Checked;

				foreach (var m in meshes)
					CurrentModel.PartsEnabled[CurrentModel.Meshes.IndexOf(m)] = item.Checked;
			}

			CalculateMatrices();
			_rendererControl.Invalidate();
		}

		private void ToggleAlphaCheckerboard()
		{
			GlobalSettings.AlphaCheckerboard = !GlobalSettings.AlphaCheckerboard;
			alphaCheckerboardToolStripMenuItem.Checked = GlobalSettings.AlphaCheckerboard;
			_rendererControl.Invalidate();
		}

		private void ToggleOverlay()
		{
			GlobalSettings.TextureOverlay = !GlobalSettings.TextureOverlay;
			textureOverlayToolStripMenuItem.Checked = GlobalSettings.TextureOverlay;
			_rendererControl.Invalidate();
		}

		private void ToggleTransparencyMode()
		{
			switch (GlobalSettings.Transparency)
			{
				case TransparencyMode.Off:
					SetTransparencyMode(TransparencyMode.Helmet);
					break;
				case TransparencyMode.Helmet:
					SetTransparencyMode(TransparencyMode.All);
					break;
				case TransparencyMode.All:
					SetTransparencyMode(TransparencyMode.Off);
					break;
			}
		}

		private void ToggleViewMode()
		{
			switch (_currentViewMode)
			{
				case ViewMode.Orthographic:
					SetViewMode(ViewMode.Perspective);
					break;
				case ViewMode.Perspective:
					SetViewMode(ViewMode.Hybrid);
					break;
				case ViewMode.Hybrid:
					SetViewMode(ViewMode.Orthographic);
					break;
			}
		}

		#region Screenshots

		private Bitmap CopyScreenToBitmap()
		{
			_rendererControl.MakeCurrent();
			_screenshotMode = true;
			rendererControl_Paint(null, null);
			_screenshotMode = false;

			var b = new Bitmap(_rendererControl.Width, _rendererControl.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			var pixels = new int[_rendererControl.Width * _rendererControl.Height];
			GL.ReadPixels(0, 0, _rendererControl.Width, _rendererControl.Height, PixelFormat.Rgba,
			              PixelType.UnsignedByte, pixels);

			BitmapData locked = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite,
			                               System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			unsafe
			{
				fixed (void* inPixels = pixels)
				{
					void* outPixels = locked.Scan0.ToPointer();

					var inInt = (int*) inPixels;
					var outInt = (int*) outPixels;

					for (int y = 0; y < b.Height; ++y)
					{
						for (int x = 0; x < b.Width; ++x)
						{
							Color color = Color.FromArgb((*inInt >> 24) & 0xFF, (*inInt >> 0) & 0xFF, (*inInt >> 8) & 0xFF,
							                             (*inInt >> 16) & 0xFF);
							*outInt = color.ToArgb();

							inInt++;
							outInt++;
						}
					}
				}
			}

			b.UnlockBits(locked);
			b.RotateFlip(RotateFlipType.RotateNoneFlipY);

			return b;
		}

		private void TakeScreenshot()
		{
			Clipboard.SetImage(CopyScreenToBitmap());
		}

		private void SaveScreenshot()
		{
			using (var sfd = new SaveFileDialog())
			{
				sfd.Filter = "PNG Image|*.png";
				sfd.RestoreDirectory = true;

				if (sfd.ShowDialog() == DialogResult.OK)
				{
					using (Bitmap bmp = CopyScreenToBitmap())
						bmp.Save(sfd.FileName);
				}
			}
		}

		#endregion

		#region Saving

		private void SetCanSave(bool value)
		{
			saveToolStripButton.Enabled = saveToolStripMenuItem.Enabled = value;
			CheckUndo();

			//treeView1.Invalidate();
		}

		private void PerformSaveAs()
		{
			Skin skin = _lastSkin;

			var grabber = new ColorGrabber(GlobalDirtiness.CurrentSkin, skin.Width, skin.Height);
			grabber.Load();

			var b = new Bitmap(skin.Width, skin.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			BitmapData locked = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite,
			                               System.Drawing.Imaging.PixelFormat.Format32bppArgb);

			unsafe
			{
				fixed (void* inPixels = grabber.Array)
				{
					void* outPixels = locked.Scan0.ToPointer();

					var inInt = (int*) inPixels;
					var outInt = (int*) outPixels;

					for (int y = 0; y < b.Height; ++y)
					{
						for (int x = 0; x < b.Width; ++x)
						{
							Color color = Color.FromArgb((*inInt >> 24) & 0xFF, (*inInt >> 0) & 0xFF, (*inInt >> 8) & 0xFF,
							                             (*inInt >> 16) & 0xFF);
							*outInt = color.ToArgb();

							inInt++;
							outInt++;
						}
					}
				}
			}

			b.UnlockBits(locked);

			using (var sfd = new SaveFileDialog())
			{
				sfd.Filter = "Skin Image|*.png";
				sfd.RestoreDirectory = true;

				if (sfd.ShowDialog() == DialogResult.OK)
					b.Save(sfd.FileName);
			}

			b.Dispose();
		}

		private void PerformSaveSkin(Skin s)
		{
			_rendererControl.MakeCurrent();

			s.CommitChanges((s == _lastSkin) ? GlobalDirtiness.CurrentSkin : s.GLImage, true);
		}

		private bool RecursiveNodeIsDirty(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				if (node is Skin)
				{
					var skin = (Skin) node;

					if (skin.Dirty)
						return true;
				}
				else if (RecursiveNodeIsDirty(node.Nodes))
					return true;
			}

			return false;
		}

		private void RecursiveNodeSave(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				if (node is Skin)
				{
					var skin = (Skin) node;

					if (skin.Dirty)
						PerformSaveSkin(skin);
				}
				else
					RecursiveNodeSave(node.Nodes);
			}
		}

		private void PerformSaveAll()
		{
			RecursiveNodeSave(treeView1.Nodes);
			treeView1.Invalidate();
		}

		private void PerformSave()
		{
			Skin skin = _lastSkin;

			if (!skin.Dirty)
				return;

			SetCanSave(false);
			PerformSaveSkin(skin);
			treeView1.Invalidate();
		}

		#endregion

		#region Skin Management

		private TreeNode _currentlyEditing;

		private void ImportSkin(string fileName, string folderLocation, TreeNode parentNode)
		{
			string name = Path.GetFileNameWithoutExtension(fileName);

			while (File.Exists(folderLocation + name + ".png"))
				name += " (New)";

			File.Copy(fileName, folderLocation + name + ".png");

			var skin = new Skin(folderLocation + name + ".png");

			if (parentNode != null)
			{
				if (!(parentNode is Skin))
					parentNode.Nodes.Add(skin);
				else
					parentNode.GetParentCollection().Add(skin);
			}
			else
				treeView1.Nodes.Add(skin);

			skin.SetImages();
			treeView1.SelectedNode = skin;
		}

		public static string GetFolderForNode(TreeNode node)
		{
			if (node == null)
				return RootFolderString;
			else if (node is Skin)
			{
				if (node.Parent == null)
				{
					if (HasOneRoot)
						return RootFolderString;

					throw new Exception();
				}
				else
					return GetFolderForNode(node.Parent);
			}
			else if (node is FolderNode)
			{
				var folder = (FolderNode) node;
				return folder.Directory.FullName;
			}

			throw new Exception();
		}

		public static string GetFolderLocationForNode(TreeNode node)
		{
			string folderLocation = "";

			if (node != null)
			{
				if (!(node is Skin))
					folderLocation = GetFolderForNode(node) + '\\';
				else if (node.Parent != null)
					folderLocation = GetFolderForNode(node.Parent) + '\\';
				else if (HasOneRoot)
					folderLocation = RootFolderString + '\\';
			}
			else
				folderLocation = RootFolderString + '\\';

			return folderLocation;
		}

		public static void GetFolderLocationAndCollectionForNode(TreeView treeView, TreeNode _rightClickedNode,
		                                                         out string folderLocation, out TreeNodeCollection collection)
		{
			folderLocation = "";
			collection = treeView.Nodes;

			if (_rightClickedNode != null)
			{
				if (!(_rightClickedNode is Skin))
				{
					folderLocation = GetFolderForNode(_rightClickedNode) + '\\';
					collection = _rightClickedNode.Nodes;
				}
				else if (_rightClickedNode.Parent != null)
				{
					folderLocation = GetFolderForNode(_rightClickedNode.Parent) + '\\';
					collection = _rightClickedNode.Parent.Nodes;
				}
				else if (HasOneRoot)
					folderLocation = RootFolderString + '\\';
			}
			else
				folderLocation = RootFolderString + '\\';
		}

		private void ImportSkins(string[] fileName, TreeNode parentNode)
		{
			string folderLocation = GetFolderLocationForNode(parentNode);

			foreach (string f in fileName)
				ImportSkin(f, folderLocation, parentNode);
		}

		private void PerformImportSkin()
		{
			if (_rightClickedNode == null)
				_rightClickedNode = treeView1.SelectedNode;

			string folderLocation = GetFolderLocationForNode(_rightClickedNode);

			if (string.IsNullOrEmpty(folderLocation))
				return;

			using (var ofd = new OpenFileDialog())
			{
				ofd.Filter = "Minecraft Skins|*.png";
				ofd.Multiselect = true;
				ofd.RestoreDirectory = true;

				if (ofd.ShowDialog() == DialogResult.OK)
					ImportSkins(ofd.FileNames, _rightClickedNode);
			}
		}

		private void PerformNewFolder()
		{
			string folderLocation;
			TreeNodeCollection collection;

			if (_rightClickedNode == null || _rightClickedNode.Parent == null)
				_rightClickedNode = treeView1.SelectedNode;

			GetFolderLocationAndCollectionForNode(treeView1, _rightClickedNode, out folderLocation, out collection);

			if (collection == null || string.IsNullOrEmpty(folderLocation))
				return;

			string newFolderName = "New Folder";

			while (Directory.Exists(folderLocation + newFolderName))
				newFolderName = newFolderName.Insert(0, GetLanguageString("C_NEW"));

			Directory.CreateDirectory(folderLocation + newFolderName);
			var newNode = new FolderNode(folderLocation + newFolderName);
			collection.Add(newNode);

			newNode.EnsureVisible();
			treeView1.SelectedNode = newNode;
			treeView1.Invalidate();

			PerformNameChange();
		}

		private void PerformNewSkin()
		{
			string folderLocation;
			TreeNodeCollection collection;

			if (_rightClickedNode == null)
				_rightClickedNode = treeView1.SelectedNode;

			GetFolderLocationAndCollectionForNode(treeView1, _rightClickedNode, out folderLocation, out collection);

			if (collection == null || string.IsNullOrEmpty(folderLocation))
				return;

			string newSkinName = "New Skin";

			while (File.Exists(folderLocation + newSkinName + ".png"))
				newSkinName = newSkinName.Insert(0, GetLanguageString("C_NEW"));

			using (var bmp = new Bitmap(64, 32))
			{
				using (Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.FromArgb(0, 255, 255, 255));

					g.FillRectangle(System.Drawing.Brushes.White, 0, 0, 32, 32);
					g.FillRectangle(System.Drawing.Brushes.White, 32, 16, 32, 16);
				}

				bmp.Save(folderLocation + newSkinName + ".png");
			}

			var newSkin = new Skin(folderLocation + newSkinName + ".png");
			collection.Add(newSkin);
			newSkin.SetImages();

			newSkin.EnsureVisible();
			treeView1.SelectedNode = newSkin;
			treeView1.Invalidate();

			PerformNameChange();
		}

		private void RecursiveDeleteSkins(TreeNode node)
		{
			foreach (TreeNode sub in node.Nodes)
			{
				if (!(sub is Skin))
					RecursiveDeleteSkins(sub);
				else
				{
					var skin = (Skin) sub;

					if (_lastSkin == skin)
						_lastSkin = null;

					skin.Dispose();
				}
			}

			Directory.Delete(GetFolderForNode(node), true);
		}

		private void PerformDeleteSkin()
		{
			if (treeView1.SelectedNode is Skin)
			{
				if (
					MessageBox.Show(this, GetLanguageString("B_MSG_DELETESKIN"), GetLanguageString("B_CAP_QUESTION"),
					                MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
				{
					var skin = (Skin) treeView1.SelectedNode;

					skin.Delete();
					skin.Remove();
					skin.Dispose();

					treeView1_AfterSelect(treeView1, new TreeViewEventArgs(treeView1.SelectedNode));

					Invalidate();

					if (_rightClickedNode == skin)
						_rightClickedNode = null;
				}
			}
			else
			{
				if (
					MessageBox.Show(this, GetLanguageString("B_MSG_DELETEFOLDER"), GetLanguageString("B_CAP_QUESTION"),
					                MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
				{
					var folder = new DirectoryInfo(GetFolderForNode(treeView1.SelectedNode));

					RecursiveDeleteSkins(treeView1.SelectedNode);

					treeView1.SelectedNode.Remove();
					treeView1_AfterSelect(treeView1, new TreeViewEventArgs(treeView1.SelectedNode));
					Invalidate();

					if (_rightClickedNode == treeView1.SelectedNode)
						_rightClickedNode = null;
				}
			}
		}

		private void PerformCloneSkin()
		{
			if (treeView1.SelectedNode == null ||
			    !(treeView1.SelectedNode is Skin))
				return;

			var skin = (Skin) treeView1.SelectedNode;
			string newName = skin.Name;
			string newFileName;

			do
			{
				newName += " - Copy";
				newFileName = skin.Directory.FullName + '\\' + newName + ".png";
			} while (File.Exists(newFileName));

			File.Copy(skin.File.FullName, newFileName);
			var newSkin = new Skin(newFileName);

			skin.GetParentCollection().Add(newSkin);

			newSkin.SetImages();
		}

		private void PerformNameChange()
		{
			if (treeView1.SelectedNode != null)
			{
				_currentlyEditing = treeView1.SelectedNode;

				if (_currentlyEditing is Skin)
					labelEditTextBox.Text = ((Skin) _currentlyEditing).Name;
				else
					labelEditTextBox.Text = _currentlyEditing.Text;

				labelEditTextBox.Show();
				labelEditTextBox.Location =
					PointToClient(
						treeView1.PointToScreen(new Point(
						                        	treeView1.SelectedNode.Bounds.Location.X + 22 + (treeView1.SelectedNode.Level * 1),
						                        	treeView1.SelectedNode.Bounds.Location.Y + 2)));
				labelEditTextBox.Size = new Size(treeView1.Width - labelEditTextBox.Location.X - 20, labelEditTextBox.Height);
				labelEditTextBox.BringToFront();
				labelEditTextBox.Focus();
			}
		}

		#endregion

		#region File uploading (FIXME: REMOVE)

		public enum ErrorCodes
		{
			Succeeded,
			TimeOut,
			WrongCredentials,
			Unknown
		}

		private readonly Login login = new Login();
		private Thread _uploadThread;

		public static Exception HttpUploadFile(string url, string file, string paramName, string contentType,
		                                       Dictionary<string, byte[]> nvc, CookieContainer cookies)
		{
			//log.Debug(string.Format("Uploading {0} to {1}", file, url));
			string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

			var wr = (HttpWebRequest) WebRequest.Create(url);
			wr.ContentType = "multipart/form-data; boundary=" + boundary;
			wr.Method = "POST";
			wr.KeepAlive = true;
			wr.CookieContainer = cookies;
			wr.Credentials = CredentialCache.DefaultCredentials;
			wr.Timeout = 10000;

			Stream rs = wr.GetRequestStream();

			const string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
			foreach (var kvp in nvc)
			{
				rs.Write(boundarybytes, 0, boundarybytes.Length);
				string formitem = string.Format(formdataTemplate, kvp.Key, Encoding.ASCII.GetString(kvp.Value));
				byte[] formitembytes = Encoding.UTF8.GetBytes(formitem);
				rs.Write(formitembytes, 0, formitembytes.Length);
			}
			rs.Write(boundarybytes, 0, boundarybytes.Length);

			const string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
			string header = string.Format(headerTemplate, paramName, Path.GetFileName(file), contentType);
			byte[] headerbytes = Encoding.UTF8.GetBytes(header);
			rs.Write(headerbytes, 0, headerbytes.Length);

			var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
			var buffer = new byte[4096];
			int bytesRead = 0;
			while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
				rs.Write(buffer, 0, bytesRead);
			fileStream.Close();

			byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
			rs.Write(trailer, 0, trailer.Length);
			rs.Close();

			WebResponse wresp = null;
			Exception ret = null;
			try
			{
				wresp = wr.GetResponse();
				Stream stream2 = wresp.GetResponseStream();
				var reader2 = new StreamReader(stream2);
				//log.Debug(string.Format("File uploaded, server response is: {0}", reader2.ReadToEnd()));
			}
			catch (Exception ex)
			{
				//log.Error("Error uploading file", ex);
				if (wresp != null)
				{
					wresp.Close();
					wresp = null;
				}

				ret = ex;
			}
			finally
			{
				wr = null;
			}

			return ret;
		}

		private void UploadThread(object param)
		{
			var parms = (object[]) param;
			var error = (ErrorReturn) parms[3];

			error.Code = ErrorCodes.Succeeded;
			error.Exception = null;
			error.ReportedError = null;

			try
			{
				var cookies = new CookieContainer();
				var request = (HttpWebRequest) WebRequest.Create("http://www.minecraft.net/login");
				request.CookieContainer = cookies;
				request.Timeout = 10000;
				WebResponse response = request.GetResponse();
				var sr = new StreamReader(response.GetResponseStream());
				string text = sr.ReadToEnd();

				Match match = Regex.Match(text, @"<input type=""hidden"" name=""authenticityToken"" value=""(.*?)"">");
				string authToken = null;
				if (match.Success)
					authToken = match.Groups[1].Value;

				if (authToken == null)
					return;

				sr.Dispose();

				response.Close();

				const string requestTemplate =
					@"authenticityToken={0}&redirect=http%3A%2F%2Fwww.minecraft.net%2Fprofile&username={1}&password={2}";
				string requestContent = string.Format(requestTemplate, authToken, parms[0], parms[1]);
				byte[] inBytes = Encoding.UTF8.GetBytes(requestContent);

				// craft the login request
				request = (HttpWebRequest) WebRequest.Create("https://www.minecraft.net/login");
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.CookieContainer = cookies;
				request.ContentLength = inBytes.Length;
				request.Timeout = 10000;

				using (Stream dataStream = request.GetRequestStream())
					dataStream.Write(inBytes, 0, inBytes.Length);

				response = request.GetResponse();
				sr = new StreamReader(response.GetResponseStream());
				text = sr.ReadToEnd();

				match = Regex.Match(text, @"<p class=""error"">([\w\W]*?)</p>");

				sr.Dispose();
				response.Close();

				if (match.Success)
				{
					error.ReportedError = match.Groups[1].Value.Trim();
					error.Code = ErrorCodes.WrongCredentials;
				}
				else
				{
					var dict = new Dictionary<string, byte[]>();
					dict.Add("authenticityToken", Encoding.ASCII.GetBytes(authToken));
					if (
						(error.Exception =
						 HttpUploadFile("http://www.minecraft.net/profile/skin", parms[2].ToString(), "skin", "image/png", dict, cookies)) !=
						null)
						error.Code = ErrorCodes.Unknown;
				}
			}
			catch (Exception ex)
			{
				error.Exception = ex;
			}
			finally
			{
				Invoke(delegate { _pleaseWaitForm.Close(); });
			}
		}

		private void PerformUpload()
		{
			if (_lastSkin == null)
				return;

			if (_lastSkin.Width != 64 || _lastSkin.Height != 32)
			{
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADRES"));
				return;
			}

			login.Username = GlobalSettings.LastUsername;
			login.Password = GlobalSettings.LastPassword;

			bool dialogRes = true;
			bool didShowDialog = false;

			if ((ModifierKeys & Keys.Shift) != 0 || !GlobalSettings.RememberMe || !GlobalSettings.AutoLogin)
			{
				login.Remember = GlobalSettings.RememberMe;
				login.AutoLogin = GlobalSettings.AutoLogin;
				dialogRes = login.ShowDialog() == DialogResult.OK;
				didShowDialog = true;
			}

			if (!dialogRes)
				return;

			_pleaseWaitForm = new PleaseWait();
			_pleaseWaitForm.FormClosed += _pleaseWaitForm_FormClosed;

			_uploadThread = new Thread(UploadThread);
			var ret = new ErrorReturn();
			_uploadThread.Start(new object[] {login.Username, login.Password, _lastSkin.File.FullName, ret});

			_pleaseWaitForm.DialogResult = DialogResult.OK;
			_pleaseWaitForm.languageProvider1.LanguageChanged(CurrentLanguage);
			_pleaseWaitForm.ShowDialog();
			_uploadThread = null;

			if (ret.ReportedError != null)
			{
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADERROR") + "\r\n" + ret.ReportedError, "Error",
				                MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else if (ret.Exception != null)
			{
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADERROR") + "\r\n" + ret.Exception.Message, "Error",
				                MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else if (_pleaseWaitForm.DialogResult != DialogResult.Abort)
			{
				MessageBox.Show(this, GetLanguageString("B_MSG_UPLOADSUCCESS"), "Woo!", MessageBoxButtons.OK,
				                MessageBoxIcon.Information);
				GlobalSettings.LastSkin = _lastSkin.File.ToString();
				treeView1.Invalidate();
			}

			if (didShowDialog)
			{
				GlobalSettings.RememberMe = login.Remember;
				GlobalSettings.AutoLogin = login.AutoLogin;

				if (GlobalSettings.RememberMe == false)
					GlobalSettings.LastUsername = GlobalSettings.LastPassword = "";
				else
				{
					GlobalSettings.LastUsername = login.Username;
					GlobalSettings.LastPassword = login.Password;
				}
			}
		}

		private void _pleaseWaitForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			_uploadThread.Abort();
		}

		private class ErrorReturn
		{
			public ErrorCodes Code;
			public Exception Exception;
			public string ReportedError;
		}

		#endregion

		#endregion

		#region Nested type: ModelToolStripMenuItem

		public class ModelToolStripMenuItem : ToolStripMenuItem
		{
			public Model Model;

			public ModelToolStripMenuItem(Model model) :
				base(model.Name)
			{
				Name = model.Name;
				Model = model;
				Model.DropDownItem = this;
			}

			protected override void OnClick(EventArgs e)
			{
				MainForm.SetModel(Model);
			}
		}

		#endregion

		#region Constructor

		ToolStripMenuItem[] _partItems;
		ToolStripButton[] _partButtons;

		public Editor()
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			MainForm = this;
			InitializeComponent();

			bool settingsLoaded = GlobalSettings.Load();

			Icon = Resources.Icon_new;

			LanguageLoader.LoadLanguages("Languages");

			DodgeBurnOptions = new DodgeBurnOptions();
			DarkenLightenOptions = new DarkenLightenOptions();
			PencilOptions = new PencilOptions();
			FloodFillOptions = new FloodFillOptions();
			NoiseOptions = new NoiseOptions();
			EraserOptions = new EraserOptions();
			StampOptions = new StampOptions();

			_tools.Add(new ToolIndex(new CameraTool(), null, "T_TOOL_CAMERA", Resources.eye__1_, Keys.C));
			_tools.Add(new ToolIndex(new PencilTool(), PencilOptions, "T_TOOL_PENCIL", Resources.pen, Keys.P));
			_tools.Add(new ToolIndex(new EraserTool(), EraserOptions, "T_TOOL_ERASER", Resources.erase, Keys.E));
			_tools.Add(new ToolIndex(new DropperTool(), null, "T_TOOL_DROPPER", Resources.pipette, Keys.D));
			_tools.Add(new ToolIndex(new DodgeBurnTool(), DodgeBurnOptions, "T_TOOL_DODGEBURN", Resources.dodge, Keys.B));
			_tools.Add(new ToolIndex(new DarkenLightenTool(), DarkenLightenOptions, "T_TOOL_DARKENLIGHTEN",
			                         Resources.darkenlighten, Keys.L));
			_tools.Add(new ToolIndex(new FloodFillTool(), FloodFillOptions, "T_TOOL_BUCKET", Resources.fill_bucket, Keys.F));
			_tools.Add(new ToolIndex(new NoiseTool(), NoiseOptions, "T_TOOL_NOISE", Resources.noise, Keys.N));
			_tools.Add(new ToolIndex(new StampTool(), StampOptions, "T_TOOL_STAMP", Resources.stamp_pattern, Keys.M));

			animateToolStripMenuItem.Checked = GlobalSettings.Animate;
			followCursorToolStripMenuItem.Checked = GlobalSettings.FollowCursor;
			grassToolStripMenuItem.Checked = GlobalSettings.Grass;
			ghostHiddenPartsToolStripMenuItem.Checked = GlobalSettings.Ghost;

			alphaCheckerboardToolStripMenuItem.Checked = GlobalSettings.AlphaCheckerboard;
			textureOverlayToolStripMenuItem.Checked = GlobalSettings.TextureOverlay;
			modeToolStripMenuItem1.Checked = GlobalSettings.OnePointEightMode;

			Language.Language useLanguage = null;
			try
			{
				// stage 1 (prelim): if no language, see if our languages contain it
				if (string.IsNullOrEmpty(GlobalSettings.LanguageFile))
				{
					useLanguage =
						LanguageLoader.FindLanguage((CultureInfo.CurrentUICulture.IsNeutralCulture == false)
						                            	? CultureInfo.CurrentUICulture.Parent.Name
						                            	: CultureInfo.CurrentUICulture.Name);
				}
				// stage 2: load from last used language
				if (useLanguage == null)
					useLanguage = LanguageLoader.FindLanguage(GlobalSettings.LanguageFile);
				// stage 3: use English file, if it exists
				if (useLanguage == null)
					useLanguage = LanguageLoader.FindLanguage("English");
			}
			catch
			{
			}
			finally
			{
				// stage 4: fallback to built-in English file
				if (useLanguage == null)
				{
					MessageBox.Show(this,
					                "For some reason, the default language files were missing or failed to load (did you extract?) - we'll supply you with a base language of English just so you know what you're doing!");
					useLanguage = LanguageLoader.LoadDefault();
				}
			}

			foreach (Language.Language lang in LanguageLoader.Languages)
			{
				lang.Item =
					new ToolStripMenuItem((lang.Culture != null)
					                      	? (char.ToUpper(lang.Culture.NativeName[0]) + lang.Culture.NativeName.Substring(1))
					                      	: lang.Name);
				lang.Item.Tag = lang;
				lang.Item.Click += languageToolStripMenuItem_Click;
				languageToolStripMenuItem.DropDownItems.Add(lang.Item);
			}

			for (int i = _tools.Count - 1; i >= 0; --i)
			{
				toolToolStripMenuItem.DropDownItems.Insert(0, _tools[i].MenuItem);
				_tools[i].MenuItem.Click += ToolMenuItemClicked;
				toolStrip1.Items.Insert(toolStrip1.Items.IndexOf(toolStripSeparator1) + 1, _tools[i].Button);
				_tools[i].Button.Click += ToolMenuItemClicked;

				languageProvider1.SetPropertyNames(_tools[i].MenuItem, "Text");
				languageProvider1.SetPropertyNames(_tools[i].Button, "Text");
			}

			InitShortcuts();
			LoadShortcutKeys(GlobalSettings.ShortcutKeys);
			_shortcutEditor.ShortcutExists += _shortcutEditor_ShortcutExists;
			CurrentLanguage = useLanguage;
			Brushes.LoadBrushes();

			SetSelectedTool(_tools[0]);

			KeyPreview = true;
			Text = "MCSkin3D v" + Program.Version.ToString();

#if BETA
			Text += " [Beta]";
#endif

			if (!Directory.Exists("Swatches"))
				MessageBox.Show(this, GetLanguageString("B_MSG_DIRMISSING"));

			Directory.CreateDirectory("Swatches");

			foreach (string x in GlobalSettings.SkinDirectories)
				Directory.CreateDirectory(x);

			_updater = new Updater("http://alteredsoftworks.com/mcskin3d/update", Program.Version.ToString());
			_updater.UpdateHandler = new AssemblyVersion();
			_updater.NewVersionAvailable += _updater_NewVersionAvailable;
			_updater.SameVersion += _updater_SameVersion;
			_updater.CheckForUpdate();

			automaticallyCheckForUpdatesToolStripMenuItem.Checked = GlobalSettings.AutoUpdate;

			SetSampleMenuItem(GlobalSettings.Multisamples);

			// set up the GL control
			var mode = new GraphicsMode();
			_rendererControl =
				new GLControl(new GraphicsMode(mode.ColorFormat, mode.Depth, mode.Stencil, GlobalSettings.Multisamples));
			_rendererControl.BackColor = Color.Black;
			_rendererControl.Dock = DockStyle.Fill;
			_rendererControl.Location = new Point(0, 25);
			_rendererControl.Name = "rendererControl";
			_rendererControl.Size = new Size(641, 580);
			_rendererControl.TabIndex = 4;
			_rendererControl.Load += rendererControl_Load;
			_rendererControl.Paint += rendererControl_Paint;
			_rendererControl.MouseDown += rendererControl_MouseDown;
			_rendererControl.MouseMove += rendererControl_MouseMove;
			_rendererControl.MouseUp += rendererControl_MouseUp;
			_rendererControl.MouseLeave += rendererControl_MouseLeave;
			_rendererControl.Resize += rendererControl_Resize;
			_rendererControl.MouseWheel += rendererControl_MouseWheel;
			_rendererControl.MouseEnter += rendererControl_MouseEnter;

			splitContainer4.Panel2.Controls.Add(_rendererControl);
			_rendererControl.BringToFront();

			var animTimer = new Timer();
			animTimer.Interval = 22;
			animTimer.Elapsed += animTimer_Elapsed;
			animTimer.Start();

			_animTimer.Elapsed += _animTimer_Elapsed;
			_animTimer.SynchronizingObject = this;

			if (!settingsLoaded)
				MessageBox.Show(this, GetLanguageString("C_SETTINGSFAILED"));

			treeView1.ItemHeight = GlobalSettings.TreeViewHeight;
			treeView1.Scrollable = true;
			splitContainer4.SplitterDistance = 74;

			ModelLoader.LoadModels();

			foreach (var x in ModelLoader.Models)
			{
				ToolStripItemCollection collection = toolStripDropDownButton1.DropDownItems;
				string path = Path.GetDirectoryName(x.Value.File.ToString()).Substring(6);

				while (!string.IsNullOrEmpty(path))
				{
					string sub = path.Substring(1, path.IndexOf('\\', 1) == -1 ? (path.Length - 1) : (path.IndexOf('\\', 1) - 1));

					ToolStripItem[] subMenu = collection.Find(sub, false);

					if (subMenu.Length == 0)
					{
						var item = ((ToolStripMenuItem) collection.Add(sub));
						item.Name = item.Text;
						collection = item.DropDownItems;
					}
					else
						collection = ((ToolStripMenuItem) subMenu[0]).DropDownItems;

					path = path.Remove(0, sub.Length + 1);
				}

				collection.Add(new ModelToolStripMenuItem(x.Value));

				// Create mesh data
				//foreach (var m in x.Value.Meshes)
				//	m.UserData = MeshRenderer.CreateUserData(m);
			}

			if (GlobalSettings.OnePointEightMode)
				ModelLoader.InvertBottomFaces();

			mLINESIZEToolStripMenuItem.NumericBox.ValueChanged += mLINESIZEToolStripMenuItem_NumericBox_ValueChanged;
			mLINESIZEToolStripMenuItem.NumericBox.Minimum = 1;
			mLINESIZEToolStripMenuItem.NumericBox.Maximum = 16;
			mLINESIZEToolStripMenuItem.NumericBox.Value = GlobalSettings.DynamicOverlayLineSize;

			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.ValueChanged +=
				mOVERLAYTEXTSIZEToolStripMenuItem_NumericBox_ValueChanged;
			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Minimum = 1;
			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Maximum = 16;
			mOVERLAYTEXTSIZEToolStripMenuItem.NumericBox.Value = GlobalSettings.DynamicOverlayTextSize;

			mGRIDOPACITYToolStripMenuItem.NumericBox.ValueChanged += mGRIDOPACITYToolStripMenuItem_NumericBox_ValueChanged;
			mGRIDOPACITYToolStripMenuItem.NumericBox.Minimum = 0;
			mGRIDOPACITYToolStripMenuItem.NumericBox.Maximum = 255;
			mGRIDOPACITYToolStripMenuItem.NumericBox.Value = GlobalSettings.DynamicOverlayGridColor.A;

			mLINECOLORToolStripMenuItem.BackColor = GlobalSettings.DynamicOverlayLineColor;
			mTEXTCOLORToolStripMenuItem.BackColor = GlobalSettings.DynamicOverlayTextColor;
			mGRIDCOLORToolStripMenuItem.BackColor = Color.FromArgb(255, GlobalSettings.DynamicOverlayGridColor);

			_undoListBox = new UndoRedoPanel();
			_undoListBox.ActionString = "L_UNDOACTIONS";
			languageProvider1.SetPropertyNames(_undoListBox, "ActionString");

			_undoListBox.ListBox.MouseClick += UndoListBox_MouseClick;

			undoToolStripButton.DropDown = new Popup(_undoListBox);
			undoToolStripButton.DropDownOpening += undoToolStripButton_DropDownOpening;

			_redoListBox = new UndoRedoPanel();
			_redoListBox.ActionString = "L_REDOACTIONS";
			languageProvider1.SetPropertyNames(_redoListBox, "ActionString");

			_redoListBox.ListBox.MouseClick += RedoListBox_MouseClick;

			redoToolStripButton.DropDown = new Popup(_redoListBox);
			redoToolStripButton.DropDownOpening += redoToolStripButton_DropDownOpening;

			undoToolStripButton.DropDown.AutoClose = redoToolStripButton.DropDown.AutoClose = true;

			mINFINITEMOUSEToolStripMenuItem.Checked = GlobalSettings.InfiniteMouse;
			mRENDERSTATSToolStripMenuItem.Checked = GlobalSettings.RenderBenchmark;
			CurrentLanguage = useLanguage;

			CreatePartList();
			_partItems = new[] { null, headToolStripMenuItem, helmetToolStripMenuItem, chestToolStripMenuItem, leftArmToolStripMenuItem, rightArmToolStripMenuItem, leftLegToolStripMenuItem, rightLegToolStripMenuItem };
			_partButtons = new[] { null, toggleHeadToolStripButton, toggleHelmetToolStripButton, toggleChestToolStripButton, toggleLeftArmToolStripButton, toggleRightArmToolStripButton, toggleLeftLegToolStripButton, toggleRightLegToolStripButton };
		}

		private void rendererControl_MouseEnter(object sender, EventArgs e)
		{
			_rendererControl.Focus();
		}

		private void _shortcutEditor_ShortcutExists(object sender, ShortcutExistsEventArgs e)
		{
			//MessageBox.Show(string.Format(GetLanguageString("B_MSG_SHORTCUTEXISTS"), e.ShortcutName, e.OtherName));
		}

		#endregion

		#region Nested type: PartTreeNode

		private class PartTreeNode : TreeNode
		{
			public PartTreeNode(Model model, Mesh mesh, int index)
			{
				Name = Text = mesh.Name;
				Model = model;
				Mesh = mesh;
				PartIndex = index;

				if (PartIndex != -1)
					PartEnabled = model.PartsEnabled[index];
			}

			public PartTreeNode(Model model, Mesh mesh, int index, string name) :
				this(model, mesh, index)
			{
				Name = Text = name;
			}

			private Model Model { get; set; }
			private Mesh Mesh { get; set; }
			private int PartIndex { get; set; }

			public bool PartEnabled
			{
				get { return Model.PartsEnabled[PartIndex]; }
				set
				{
					Model.PartsEnabled[PartIndex] = value;
					SelectedImageIndex = ImageIndex = (value) ? 3 : 0;

					for (TreeNode node = Parent; node != null; node = node.Parent)
						((PartTreeNode) node).CheckGroupImage();
				}
			}

			private void RecursiveGroupImageCheck(PartTreeNode node, ref bool hasEnabled, ref bool hasDisabled)
			{
				if (hasEnabled && hasDisabled)
					return;

				if (node.Nodes.Count == 0)
				{
					if (node.PartEnabled)
						hasEnabled = true;
					else
						hasDisabled = true;
				}
				else
				{
					foreach (PartTreeNode n in node.Nodes)
						RecursiveGroupImageCheck(n, ref hasEnabled, ref hasDisabled);
				}
			}

			public void CheckGroupImage()
			{
				bool hasEnabled = false, hasDisabled = false;

				foreach (PartTreeNode node in Nodes)
					RecursiveGroupImageCheck(node, ref hasEnabled, ref hasDisabled);

				if (hasEnabled && hasDisabled)
					SelectedImageIndex = ImageIndex = 6;
				else if (hasEnabled)
					SelectedImageIndex = ImageIndex = 3;
				else if (hasDisabled)
					SelectedImageIndex = ImageIndex = 0;
			}
		}

		#endregion
	}
}