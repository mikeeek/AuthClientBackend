using System.Net.Http.Headers;   // for AuthenticationHeaderValue
using System.Net.Http.Json;

namespace AuthApp
{
    public partial class Form1 : Form
    {
        private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5080") };

        //token + expiry in memory for subsequent authorized calls
        private string? _accessToken;
        private DateTime _tokenExpiresAtUtc;

        public Form1()
        {
            InitializeComponent();

            buttonLogin.Click += async (_, __) => await DoLoginAsync();
            buttonRegister.Click += async (_, __) => await DoRegisterAsync();
        }

        private async Task DoRegisterAsync()
        {
            var username = textBoxUser.Text.Trim();
            var password = textBoxPass.Text;
            var licenseKey = textBoxKey.Text.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Please enter username and password.");
                return;
            }

            var payload = new
            {
                username,
                password,
                licenseKey = string.IsNullOrWhiteSpace(licenseKey) ? null : licenseKey
            };

            try
            {
                buttonRegister.Enabled = false;
                var resp = await _http.PostAsJsonAsync("/register", payload);

                if (resp.IsSuccessStatusCode)
                {
                    var msg = string.IsNullOrWhiteSpace(licenseKey)
                        ? "Registration successful!\n(No license key was provided.)"
                        : $"Registration successful!\nClaimed key: {licenseKey}";
                    MessageBox.Show(msg);
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    var serverMsg = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Conflict: {serverMsg}");
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var serverMsg = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Bad request: {serverMsg}");
                }
                else
                {
                    var serverMsg = await resp.Content.ReadAsStringAsync();
                    MessageBox.Show($"Registration failed: {(int)resp.StatusCode} {serverMsg}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
            finally
            {
                buttonRegister.Enabled = true;
            }
        }

        private async Task DoLoginAsync()
        {
            var payload = new
            {
                username = textBoxUser.Text,
                password = textBoxPass.Text,
                key = textBoxKey.Text
            };

            try
            {
                var resp = await _http.PostAsJsonAsync("/auth/check", payload);

                if (resp.IsSuccessStatusCode)
                {
                    var data = await resp.Content.ReadFromJsonAsync<AuthOk>();
                    if (data is null || string.IsNullOrWhiteSpace(data.accessToken))
                    {
                        MessageBox.Show("Login succeeded but token missing.");
                        return;
                    }

                    // Store token + expiry
                    _accessToken = data.accessToken;
                    _tokenExpiresAtUtc = data.tokenExpiresAtUtc;

                    // Set Authorization header for future requests
                    _http.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _accessToken);


                    MessageBox.Show($"Auth OK: {data.username} ({data.level})");

                    //  Open the authenticated window and hide the login form
                    var meForm = new MeForm(_http, _tokenExpiresAtUtc, data.username, data.level);

                    this.Hide();
                    meForm.FormClosed += (_, __) =>
                    {
                        // Optional: clear token on close if you want a logout effect
                        // _http.DefaultRequestHeaders.Authorization = null;
                        // _accessToken = null;

                        this.Show();
                        this.Activate();
                    };
                    meForm.Show();
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    MessageBox.Show("Invalid username or password.");
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    MessageBox.Show("Invalid or expired license.");
                }
                else
                {
                    MessageBox.Show($"Error: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Request failed: {ex.Message}");
            }
        }

        // Matches API /auth/check response
        private record AuthOk(
            string username,
            string licenseKey,
            string level,
            DateTime subscriptionExpiresAt,
            string accessToken,
            string tokenType,
            DateTime tokenIssuedAtUtc,
            DateTime tokenExpiresAtUtc,
            int tokenExpiresInSeconds
        );
    }
}
