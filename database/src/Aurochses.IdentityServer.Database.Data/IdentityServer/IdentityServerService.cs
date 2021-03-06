﻿using Aurochses.IdentityServer.Database.Context;
using Aurochses.IdentityServer.Database.Data.IdentityServer.Data;
using IdentityServer4.EntityFramework.Mappers;

namespace Aurochses.IdentityServer.Database.Data.IdentityServer
{
    public class IdentityServerService
    {
        private readonly IdentityServerConfigurationContext _context;

        public IdentityServerService(IdentityServerConfigurationContext context)
        {
            _context = context;
        }

        public void Run(string environmentName)
        {
            // IdentityResource
            _context.RemoveRange(_context.IdentityResources);
            _context.SaveChanges();

            foreach (var item in IdentityResourceData.GetList(environmentName))
            {
                _context.IdentityResources.Add(item.ToEntity());
            }
            _context.SaveChanges();

            // ApiResource
            _context.RemoveRange(_context.ApiResources);
            _context.SaveChanges();

            foreach (var item in ApiResourceData.GetList(environmentName))
            {
                _context.ApiResources.Add(item.ToEntity());
            }
            _context.SaveChanges();

            // Client
            _context.RemoveRange(_context.Clients);
            _context.SaveChanges();

            foreach (var item in ClientData.GetList(environmentName))
            {
                _context.Clients.Add(item.ToEntity());
            }
            _context.SaveChanges();
        }
    }
}
