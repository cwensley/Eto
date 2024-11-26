using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Eto.Test.Sections.Behaviors
{
	[Section("Behaviors", typeof(Themes))]
    public class ThemeSection : Panel
    {
		public ThemeSection()
		{
			var themes = Themes.AllThemes.ToList();
			// themes.Insert(0, null);
			var themeDropDown = new DropDown
			{
				DataStore = themes,
				ItemKeyBinding = Binding.Delegate((Theme t) => t.Name),
				ItemTextBinding = Binding.Delegate((Theme t) => GetThemeName(t))
			};
			themeDropDown.SelectedValueBinding.Bind(Application.Instance, t => t.CurrentTheme);


			var currentThemeLabel = new Label();
			currentThemeLabel.TextBinding.Bind(Application.Instance, t => t.CurrentTheme.Name);

			var currentThemeStyleLabel = new Label();
			currentThemeStyleLabel.TextBinding.Bind(Application.Instance, Binding.Property((Application t) => t.CurrentTheme.ThemeStyle).Convert(r => r.ToString()));

			var layout = new DynamicLayout { DefaultSpacing = new Size(5, 5) };
			layout.BeginCentered(yscale: true);
			layout.AddAutoSized(themeDropDown);
			layout.AddSeparateRow("Current Theme:", currentThemeLabel);
			layout.AddSeparateRow("Current Theme Style:", currentThemeStyleLabel);
			layout.EndCentered();

			Content = layout;
		}

		private string GetThemeName(Theme t)
		{
			if (t == null)
				return null;
			if (Themes.Light == t)
				return $"{t.Name} (Themes.Light)";
			if (Themes.Dark == t)
				return $"{t.Name} (Themes.Dark)";
			return t.Name;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			Application.Instance.CurrentThemeChanged += OnCurrentThemeChanged;
		}

		private void OnCurrentThemeChanged(object sender, EventArgs e)
		{
			var theme = Application.Instance.CurrentTheme;
			Log.Write(this, $"CurrentThemeChanged: {theme.Name}, Style: {theme.ThemeStyle}");
		}

		protected override void OnUnLoad(EventArgs e)
		{
			base.OnUnLoad(e);
			Application.Instance.CurrentThemeChanged -= OnCurrentThemeChanged;
		}

	}
}