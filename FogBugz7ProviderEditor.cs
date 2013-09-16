using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Web.Controls;
using Inedo.BuildMaster.Web.Controls.Extensions;
using Inedo.Web.Controls;

namespace Inedo.BuildMasterExtensions.FogBugz
{
    /// <summary>
    /// Custom editor for the FogBugz Issue Tracking Provider.
    /// </summary>
    internal sealed class FogBugz7ProviderEditor : ProviderEditorBase
    {
        private ValidatingTextBox txtFogBugzApiUrl;
        private ValidatingTextBox txtUserEmail;
        private PasswordTextBox txtPassword;

        /// <summary>
        /// Initializes a new instance of the <see cref="FogBugz7ProviderEditor"/> class.
        /// </summary>
        public FogBugz7ProviderEditor()
        {
        }

        public override void BindToForm(ProviderBase extension)
        {
            this.EnsureChildControls();

            var provider = (FogBugz7Provider)extension;
            this.txtFogBugzApiUrl.Text = provider.FogBugzApiUrl ?? string.Empty;
            this.txtUserEmail.Text = provider.UserEmail ?? string.Empty;
            this.txtPassword.Text = provider.Password ?? string.Empty;
        }
        public override ProviderBase CreateFromForm()
        {
            this.EnsureChildControls();

            return new FogBugz7Provider
            {
                FogBugzApiUrl = this.txtFogBugzApiUrl.Text,
                UserEmail = this.txtUserEmail.Text,
                Password = this.txtPassword.Text
            };
        }

        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            this.txtFogBugzApiUrl = new ValidatingTextBox
            {
                Width = 300,
                Required = true
            };

            this.txtUserEmail = new ValidatingTextBox
            {
                Width = 300,
                Required = true
            };

            this.txtPassword = new PasswordTextBox
            {
                Width = 270
            };

            CUtil.Add(this,
                new FormFieldGroup(
                    "FogBugz URL",
                    "Provide the URL of the FogBugz API. For example: http://fogbugz/api.xml",
                    false,
                    new StandardFormField(
                        "FogBugz API URL:",
                        this.txtFogBugzApiUrl
                    )
                ),
                new FormFieldGroup(
                    "FogBugz Credentials",
                    "Specify the e-mail address and password of the account which BuildMaster will use to connect to FogBugz.",
                    false,
                    new StandardFormField(
                        "User E-mail Address:",
                        this.txtUserEmail
                    ),
                    new StandardFormField(
                        "Password:",
                        this.txtPassword
                    )
                )
            );
        }
    }
}
