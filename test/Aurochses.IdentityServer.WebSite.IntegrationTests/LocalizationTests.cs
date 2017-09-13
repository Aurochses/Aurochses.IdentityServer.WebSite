﻿using System.Collections.Generic;
using System.Reflection;
using Aurochses.Testing;
using Aurochses.Testing.Mvc.Localization;
using Xunit;

namespace Aurochses.IdentityServer.WebSite.IntegrationTests
{
    public class LocalizationTests
    {
        private readonly string _projectPath;

        public LocalizationTests()
        {
            _projectPath = ProjectHelpers.GetFolderPath("Aurochses.IdentityServer.WebSite", "src", "Aurochses.IdentityServer.WebSite");
        }

        [Fact]
        public void Controllers_Success()
        {
            // Arrange
            var predefinedLocalizedFileItems = new List<LocalizedFileItem>
            {
                new LocalizedFileItem(_projectPath, @"\Controllers", "", "TwoFactorSignInController.cs")
                {
                    Names = {"Email", "Phone"}
                }
            };

            // Act & Assert
            ControllerLocalizationAssert.Validate(_projectPath, predefinedLocalizedFileItems: predefinedLocalizedFileItems);
        }

        [Fact]
        public void Models_Success()
        {
            // Arrange & Act & Assert
            ModelLocalizationAssert.Validate(typeof(WebSite.Startup).GetTypeInfo().Assembly, _projectPath);
        }

        [Fact]
        public void Views_Success()
        {
            // Arrange & Act & Assert
            ViewLocalizationAssert.Validate(_projectPath);
        }
    }
}