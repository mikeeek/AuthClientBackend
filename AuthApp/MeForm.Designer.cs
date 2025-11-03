namespace AuthApp
{
    partial class MeForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label labelHeader;
        private Button buttonFetchMe;
        private TextBox textBoxOutput;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelHeader = new Label();
            this.buttonFetchMe = new Button();
            this.textBoxOutput = new TextBox();
            this.SuspendLayout();
            // 
            // labelHeader
            // 
            this.labelHeader.AutoSize = true;
            this.labelHeader.Location = new Point(20, 18);
            this.labelHeader.Name = "labelHeader";
            this.labelHeader.Size = new Size(170, 15);
            this.labelHeader.Text = "Logged in as: (not initialized)";
            // 
            // buttonFetchMe
            // 
            this.buttonFetchMe.Location = new Point(20, 50);
            this.buttonFetchMe.Name = "buttonFetchMe";
            this.buttonFetchMe.Size = new Size(140, 32);
            this.buttonFetchMe.Text = "Fetch /me";
            this.buttonFetchMe.UseVisualStyleBackColor = true;
            this.buttonFetchMe.Click += new EventHandler(this.buttonFetchMe_Click);
            // 
            // textBoxOutput
            // 
            this.textBoxOutput.Location = new Point(20, 95);
            this.textBoxOutput.Multiline = true;
            this.textBoxOutput.ScrollBars = ScrollBars.Vertical;
            this.textBoxOutput.ReadOnly = true;
            this.textBoxOutput.Size = new Size(520, 260);
            this.textBoxOutput.Name = "textBoxOutput";
            // 
            // MeForm
            // 
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(564, 372);
            this.Controls.Add(this.textBoxOutput);
            this.Controls.Add(this.buttonFetchMe);
            this.Controls.Add(this.labelHeader);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Authenticated Area";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
