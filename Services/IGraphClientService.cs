// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Graph;

namespace GraphNotifications.Services
{
    public interface IGraphClientService
    {
        GraphServiceClient GetUserGraphClient(string userAssertion);
    }
}
