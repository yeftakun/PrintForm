using System;
using System.Windows.Forms;

namespace PrintForm
{
    internal sealed class SafePrintPreviewControl : PrintPreviewControl
    {
        protected override void OnResize(EventArgs eventargs)
        {
            try
            {
                base.OnResize(eventargs);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Workaround for WinForms PrintPreviewControl LayoutScrollBars bug.
            }
        }
    }
}
