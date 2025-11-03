using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace AuthApp
{
    public partial class MeForm : Form
    {
        private readonly HttpClient _http;
        private readonly DateTime _tokenExpiresAtUtc;

        public MeForm(HttpClient http, DateTime tokenExpiresAtUtc, string username, string level)
        {
            InitializeComponent();
            _http = http;
            _tokenExpiresAtUtc = tokenExpiresAtUtc;

            labelHeader.Text = $"Logged in as: {username}  •  Level: {level}";
        }

        private async void buttonFetchMe_Click(object? sender, EventArgs e)
        {
            try
            {
                if (DateTime.UtcNow >= _tokenExpiresAtUtc)  // small grace window
                {
                    MessageBox.Show("Token expired. Please login again.");
                    return;
                }

                // Authorization header is already set by Form1 after login,
                // but re-assert just in case your app changes it elsewhere.
                if (_http.DefaultRequestHeaders.Authorization is null)
                {
                    MessageBox.Show("No Authorization header set. Please login again.");
                    return;
                }

                buttonFetchMe.Enabled = false;
                textBoxOutput.Text = "Calling /me ...";

                var resp = await _http.GetAsync("/me");
                var body = await resp.Content.ReadAsStringAsync();

                textBoxOutput.Text = $"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}\r\n\r\n{body}";
            }
            catch (Exception ex)
            {
                textBoxOutput.Text = $"Error: {ex.Message}";
            }
            finally
            {
                buttonFetchMe.Enabled = true;
            }
        }
    }
}
