// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace GraphNotifications.Models
{
    public class SubscriptionDefinition
    {
        public string Resource {get; set;}
        public DateTimeOffset ExpirationTime {get;set;}
        public string[] ChangeTypes {get; set;}
        public bool ResourceData {get; set;}
    }
}