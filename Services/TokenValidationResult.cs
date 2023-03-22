// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Security.Claims;

namespace GraphNotifications.Services
{
    public class TokenValidationResult
    {
        // User ID
        public string Upn { get; private set; }
        public string UserId { get; private set; }
        public string TenantId { get; private set; }
        public string AppId {get; private set;}

        // The extracted token - used to build user assertion
        // for OBO flow
        public string Token { get; private set; }

        public IList<Claim> Claims { get; private set; }

        public TokenValidationResult(string token, IList<Claim> claims)
        {
            Token = token;
            Claims = claims;

            if(claims.Where(x => x.Type == "upn").Count() > 0)
                Upn = claims.First(x => x.Type == "upn").Value;
            if(claims.Where(x => x.Type == "oid").Count() > 0)
                UserId = claims.First(x => x.Type == "oid").Value;
            if(claims.Where(x => x.Type == "tid").Count() > 0)
                TenantId = claims.First(x => x.Type == "tid").Value;
            if(claims.Where(x => x.Type == "appid").Count() > 0)
                AppId = claims.First(x => x.Type == "appid").Value;
        }
    }
}
