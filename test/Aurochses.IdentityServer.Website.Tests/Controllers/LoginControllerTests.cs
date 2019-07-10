﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aurochses.AspNetCore.Identity.EntityFrameworkCore;
using Aurochses.IdentityServer.Website.Controllers;
using Aurochses.IdentityServer.Website.Filters;
using Aurochses.IdentityServer.Website.Models.Login;
using Aurochses.IdentityServer.Website.Models.Shared;
using Aurochses.IdentityServer.Website.Options;
using Aurochses.IdentityServer.Website.Tests.Fakes;
using Aurochses.Xunit;
using Aurochses.Xunit.AspNetCore.Mvc;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aurochses.IdentityServer.Website.Tests.Controllers
{
    public class LoginControllerTests : ControllerTestsBase<LoginController>
    {
        private const string WindowsAuthenticationSchemeName = "Test WindowsAuthenticationSchemeName";

        private readonly Mock<IIdentityServerInteractionService> _mockIdentityServerInteractionService;
        private readonly Mock<IAuthenticationSchemeProvider> _mockAuthenticationSchemeProvider;
        private readonly Mock<IClientStore> _mockClientStore;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<IEventService> _mockEventService;

        private readonly LoginController _controller;

        public LoginControllerTests()
        {
            var mockAccountOptions = new Mock<IOptions<AccountOptions>>(MockBehavior.Strict);
            mockAccountOptions
                .SetupGet(x => x.Value)
                .Returns(
                    new AccountOptions
                    {
                        AllowLocalLogin = true,
                        AllowRememberLogin = true,
                        WindowsAuthenticationSchemeName = WindowsAuthenticationSchemeName
                    }
                );

            _mockIdentityServerInteractionService = new Mock<IIdentityServerInteractionService>(MockBehavior.Strict);
            _mockAuthenticationSchemeProvider = new Mock<IAuthenticationSchemeProvider>(MockBehavior.Strict);
            _mockClientStore = new Mock<IClientStore>(MockBehavior.Strict);
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(new Mock<IUserStore<ApplicationUser>>().Object, null, null, null, null, null, null, null, null);
            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(_mockUserManager.Object, new Mock<IHttpContextAccessor>().Object, new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object, new Mock<IOptions<IdentityOptions>>().Object, new Mock<ILogger<SignInManager<ApplicationUser>>>().Object, new Mock<IAuthenticationSchemeProvider>().Object);
            _mockEventService = new Mock<IEventService>(MockBehavior.Strict);

            _controller = new LoginController(
                MockLogger.Object,
                mockAccountOptions.Object,
                _mockIdentityServerInteractionService.Object,
                _mockAuthenticationSchemeProvider.Object,
                _mockClientStore.Object,
                _mockSignInManager.Object,
                _mockUserManager.Object,
                _mockEventService.Object
            );
        }

        [Theory]
        [InlineData(typeof(SecurityHeadersAttribute))]
        [InlineData(typeof(AllowAnonymousAttribute))]
        public void Attribute_Defined(Type attributeType)
        {
            // Arrange & Act & Assert
            TypeAssert.HasAttribute<LoginController>(attributeType);
        }

        [Fact]
        public void Inherit_Controller()
        {
            // Arrange & Act & Assert
            Assert.IsAssignableFrom<Controller>(_controller);
        }

        #region IndexGet

        [Theory]
        [InlineData(typeof(HttpGetAttribute))]
        public void IndexGet_Attribute_Defined(Type attributeType)
        {
            // Arrange & Act & Assert
            TypeAssert.MethodHasAttribute<LoginController>("Index", new[] { typeof(string) }, attributeType);
        }

        [Fact]
        public async Task IndexGet_WhenContextIdPIsNotNull_ReturnRedirectToActionResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string idP = "Test IdP";

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(
                    new AuthorizationRequest
                    {
                        IdP = idP
                    }
                );

            // Act
            var actionResult = await _controller.Index(returnUrl);

            // Assert
            VerifyLogger(LogLevel.Information, Times.Once);

            MvcAssert.RedirectToActionResult(
                actionResult,
                "Challenge",
                "External",
                new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("Provider", idP),
                    new KeyValuePair<string, object>("ReturnUrl", returnUrl)
                }
            );
        }

        [Fact]
        public async Task IndexGet_WhenContextClientIdIsNotNull_ReturnViewResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string clientId = "Test ClientId";
            const string loginHint = "Test LoginHint";

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(
                    new AuthorizationRequest
                    {
                        ClientId = clientId,
                        LoginHint = loginHint
                    }
                );

            _mockAuthenticationSchemeProvider
                .Setup(x => x.GetAllSchemesAsync())
                .ReturnsAsync(
                    new List<AuthenticationScheme>
                    {
                        new AuthenticationScheme("Test Name", "Test DisplayName", typeof(IAuthenticationHandler)),
                        new AuthenticationScheme(WindowsAuthenticationSchemeName, null, typeof(IAuthenticationHandler))
                    }
                );

            _mockClientStore
                .Setup(x => x.FindClientByIdAsync(clientId))
                .ReturnsAsync(
                    new Client
                    {
                        Enabled = true,
                        EnableLocalLogin = true,
                        IdentityProviderRestrictions = new List<string>
                        {
                            WindowsAuthenticationSchemeName
                        }
                    }
                );

            var expectedViewModel = new LoginViewModel
            {
                UserName = loginHint,
                ReturnUrl = returnUrl,

                AllowRememberLogin = true,
                EnableLocalLogin = true,

                ExternalProviders = new[]
                {
                    new ExternalProvider
                    {
                        DisplayName = null,
                        AuthenticationScheme = WindowsAuthenticationSchemeName
                    }
                }
            };

            // Act
            var actionResult = await _controller.Index(returnUrl);

            // Assert
            MvcAssert.ViewResult(actionResult, model: expectedViewModel);
        }

        [Fact]
        public async Task IndexGet_WhenContextClientIdIsNotNull_And_ClientEnableLocalLoginIsFalse_ReturnRedirectToActionResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string clientId = "Test ClientId";
            const string loginHint = "Test LoginHint";

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(
                    new AuthorizationRequest
                    {
                        ClientId = clientId,
                        LoginHint = loginHint
                    }
                );

            _mockAuthenticationSchemeProvider
                .Setup(x => x.GetAllSchemesAsync())
                .ReturnsAsync(
                    new List<AuthenticationScheme>
                    {
                        new AuthenticationScheme("Test Name", "Test DisplayName", typeof(IAuthenticationHandler)),
                        new AuthenticationScheme(WindowsAuthenticationSchemeName, null, typeof(IAuthenticationHandler))
                    }
                );

            _mockClientStore
                .Setup(x => x.FindClientByIdAsync(clientId))
                .ReturnsAsync(
                    new Client
                    {
                        Enabled = true,
                        EnableLocalLogin = false,
                        IdentityProviderRestrictions = new List<string>
                        {
                            WindowsAuthenticationSchemeName
                        }
                    }
                );

            // Act
            var actionResult = await _controller.Index(returnUrl);

            // Assert
            VerifyLogger(LogLevel.Information, Times.Once);

            MvcAssert.RedirectToActionResult(
                actionResult,
                "Challenge",
                "External",
                new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("Provider", WindowsAuthenticationSchemeName),
                    new KeyValuePair<string, object>("ReturnUrl", returnUrl)
                }
            );
        }

        [Fact]
        public async Task IndexGet_WhenContextClientIdIsNotNull_And_IdentityProviderRestrictionsIsNull_ReturnViewResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string clientId = "Test ClientId";
            const string loginHint = "Test LoginHint";

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(
                    new AuthorizationRequest
                    {
                        ClientId = clientId,
                        LoginHint = loginHint
                    }
                );

            _mockAuthenticationSchemeProvider
                .Setup(x => x.GetAllSchemesAsync())
                .ReturnsAsync(
                    new List<AuthenticationScheme>
                    {
                        new AuthenticationScheme("Test Name", "Test DisplayName", typeof(IAuthenticationHandler)),
                        new AuthenticationScheme(WindowsAuthenticationSchemeName, null, typeof(IAuthenticationHandler))
                    }
                );

            _mockClientStore
                .Setup(x => x.FindClientByIdAsync(clientId))
                .ReturnsAsync(
                    new Client
                    {
                        Enabled = true,
                        EnableLocalLogin = true,
                        IdentityProviderRestrictions = null
                    }
                );

            var expectedViewModel = new LoginViewModel
            {
                UserName = loginHint,
                ReturnUrl = returnUrl,

                AllowRememberLogin = true,
                EnableLocalLogin = true,

                ExternalProviders = new[]
                {
                    new ExternalProvider
                    {
                        DisplayName = "Test DisplayName",
                        AuthenticationScheme = "Test Name"
                    },
                    new ExternalProvider
                    {
                        DisplayName = null,
                        AuthenticationScheme = WindowsAuthenticationSchemeName
                    }
                }
            };

            // Act
            var actionResult = await _controller.Index(returnUrl);

            // Assert
            MvcAssert.ViewResult(actionResult, model: expectedViewModel);
        }

        [Fact]
        public async Task IndexGet_WhenContextIsNull_ReturnViewResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(() => null);

            _mockAuthenticationSchemeProvider
                .Setup(x => x.GetAllSchemesAsync())
                .ReturnsAsync(
                    new List<AuthenticationScheme>
                    {
                        new AuthenticationScheme("Test Name", "Test DisplayName", typeof(IAuthenticationHandler)),
                        new AuthenticationScheme(WindowsAuthenticationSchemeName, null, typeof(IAuthenticationHandler))
                    }
                );

            var expectedViewModel = new LoginViewModel
            {
                UserName = null,
                ReturnUrl = returnUrl,

                AllowRememberLogin = true,
                EnableLocalLogin = true,

                ExternalProviders = new[]
                {
                    new ExternalProvider
                    {
                        DisplayName = "Test DisplayName",
                        AuthenticationScheme = "Test Name"
                    },
                    new ExternalProvider
                    {
                        DisplayName = null,
                        AuthenticationScheme = WindowsAuthenticationSchemeName
                    }
                }
            };

            // Act
            var actionResult = await _controller.Index(returnUrl);

            // Assert
            MvcAssert.ViewResult(actionResult, model: expectedViewModel);
        }

        #endregion

        #region IndexPost

        [Theory]
        [InlineData(typeof(HttpPostAttribute))]
        [InlineData(typeof(ValidateAntiForgeryTokenAttribute))]
        public void IndexPost_Attribute_Defined(Type attributeType)
        {
            // Arrange & Act & Assert
            TypeAssert.MethodHasAttribute<LoginController>("Index", new[] { typeof(LoginInputModel), typeof(string) }, attributeType);
        }

        [Fact]
        public async Task IndexPost_WhenButtonIsNotLogin_And_ContextIsNotNull_And_IsPkceClientIsTrue_ReturnViewResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string button = "Not login";
            const string clientId = "Test ClientId";

            var loginInputModel = new LoginInputModel
            {
                ReturnUrl = returnUrl
            };

            var context = new AuthorizationRequest
            {
                ClientId = clientId
            };

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(context);

            _mockIdentityServerInteractionService
                .Setup(x => x.GrantConsentAsync(context, ConsentResponse.Denied, null))
                .Returns(Task.CompletedTask)
                .Verifiable();

            _mockClientStore
                .Setup(x => x.FindClientByIdAsync(clientId))
                .ReturnsAsync(
                    new Client
                    {
                        RequirePkce = true
                    }
                );

            var expectedViewModel = new RedirectViewModel
            {
                RedirectUrl = returnUrl
            };

            // Act
            var actionResult = await _controller.Index(loginInputModel, button);

            // Assert
            _mockIdentityServerInteractionService
                .Verify(x => x.GrantConsentAsync(context, ConsentResponse.Denied, null), Times.Once);

            MvcAssert.ViewResult(actionResult, "Redirect", expectedViewModel);
        }

        [Fact]
        public async Task IndexPost_WhenButtonIsNotLogin_And_ContextIsNotNull_And_IsPkceClientIsFalse_ReturnViewResult()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string button = "Not login";
            const string clientId = "Test ClientId";

            var loginInputModel = new LoginInputModel
            {
                ReturnUrl = returnUrl
            };

            var context = new AuthorizationRequest
            {
                ClientId = clientId
            };

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(context);

            _mockIdentityServerInteractionService
                .Setup(x => x.GrantConsentAsync(context, ConsentResponse.Denied, null))
                .Returns(Task.CompletedTask)
                .Verifiable();

            _mockClientStore
                .Setup(x => x.FindClientByIdAsync(clientId))
                .ReturnsAsync(
                    new Client
                    {
                        RequirePkce = false
                    }
                );

            // Act
            var actionResult = await _controller.Index(loginInputModel, button);

            // Assert
            _mockIdentityServerInteractionService
                .Verify(x => x.GrantConsentAsync(context, ConsentResponse.Denied, null), Times.Once);

            MvcAssert.RedirectResult(
                actionResult,
                returnUrl
            );
        }

        [Fact]
        public async Task IndexPost_WhenButtonIsNotLogin_And_ContextIsNull_ReturnRedirectToAction()
        {
            // Arrange
            const string returnUrl = "Test ReturnUrl";
            const string button = "Not login";

            var loginInputModel = new LoginInputModel
            {
                ReturnUrl = returnUrl
            };

            _mockIdentityServerInteractionService
                .Setup(x => x.GetAuthorizationContextAsync(returnUrl))
                .ReturnsAsync(() => null);

            // Act
            var actionResult = await _controller.Index(loginInputModel, button);

            // Assert
            MvcAssert.RedirectToActionResult(
                actionResult,
                "Index",
                "Home"
            );
        }

        #endregion
    }
}