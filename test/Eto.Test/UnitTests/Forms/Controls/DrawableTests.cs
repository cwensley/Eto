using NUnit.Framework;

namespace Eto.Test.UnitTests.Forms.Controls
{
	[TestFixture]
	public class DrawableTests : TestBase
	{
		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void InvalidateShouldRepaintAfterShown(bool inContainers, bool invalidateParent)
		{
			int paints = 0;
			int before = -1, after = -1;
			Form(form =>
			{
				var drawable = new Drawable { Size = new Size(100, 100) };
				drawable.Paint += (sender, e) => paints++;
				Control content = inContainers ? new TableLayout(new TableRow(new Panel { Content = drawable })) : drawable;
				form.Content = content;
				form.Shown += async (sender, e) =>
				{
					await Task.Delay(300);
					before = paints;
					for (int i = 0; i < 3; i++)
					{
						if (invalidateParent)
							content.Invalidate();
						else
							drawable.Invalidate();
						await Task.Delay(200);
					}
					after = paints;
					form.Close();
				};
			});
			Assert.That(after, Is.GreaterThanOrEqualTo(before + 3), "Each Invalidate should cause a paint");
		}

		[Test, ManualTest]
		public void DrawableWithCanFocusShouldGetFirstMouseDownOnInactiveWindow()
		{
			bool wasClicked = false;
			bool gotFocusBeforeClick = false;
			Form(form =>
			{

				var drawable = new Drawable();
				var font = SystemFonts.Default();
				drawable.Paint += (sender, e) =>
				{
					e.Graphics.FillRectangle(Colors.Blue, 0, 0, drawable.Width, drawable.Height);
					e.Graphics.DrawText(font, SystemColors.ControlText, 0, 0, "Clicking once on this control should close the form");
				};
				drawable.Size = new Size(350, 200);
				drawable.CanFocus = true;
				drawable.MouseDown += (sender, e) =>
				{
					wasClicked = true;
					form.Close();
				};
				form.Content = drawable;
				form.GotFocus += (sender, e) =>
				{
					Application.Instance.AsyncInvoke(() =>
					{
						if (!wasClicked)
							gotFocusBeforeClick = true;
					});
				};

				form.ShowActivated = false;
				form.Owner = Application.Instance.MainForm;
			}, -1);

			Assert.That(wasClicked, Is.True, "#1 Drawable didn't get clicked");
			Assert.That(gotFocusBeforeClick, Is.False, "#2 Form should not have got focus before MouseDown event");
		}

		[Test]
		public void DrawableWithWrappedLabelShouldAutoSizeToConstraints()
		{
			Form form = null;
			Label labelThatShouldWrap = null;
			Shown(f =>
			{
				form = f;
				labelThatShouldWrap = new Label
				{
					Text = "This is some label that should wrap and show you the entire string including the period at the end."
				};

				var drawable = new Drawable
				{
					Content = labelThatShouldWrap
				};

				form.Content = drawable;
				form.Width = 100;
				form.WindowStyle = WindowStyle.Utility;
			},
			() =>
			{
				Assert.That(labelThatShouldWrap.Height, Is.GreaterThanOrEqualTo(20), "#1 - Label should have wrapped!");

				Assert.That(form.Width, Is.EqualTo(100), "#2 - Form width should be 100");
			}
			);
		}

	}
}