# Graph Notification Broker

|  [Architecture](/docs/architecture.md#overall-architecture) |  [Deployment Guide](/docs/deployment-guide.md#Deployment-Guide) |
| ---- | ---- |

[MS Graph](https://learn.microsoft.com/en-us/graph/overview) provides an easy way to access the data in Microsoft 365, including information about users, documents, meetings, and more. When data changes, timely notifications are needed. For example, a chat application needs to know when someone is available or out of the office. This asset simplifies the development complexity through immediate notice of Graph changes.

Graph Notification Broker (GNB) enables apps to receive change notifications from MS Graph in real time without the application having to poll repeatedly. GNB offers a pub/sub service of notifications which reduces the load on Graph API and provides for automatic renewal of the subscription to ensure delivery of notifications. GNB is architected to function even when the apps are behind a private network.

For collaborative applications, it is important to be able to be notified on changes in Microsoft 365 entities. Examples include:

* The arrival of an email,
* A reply on a chat,
* A change in "presence" of a user
* A change to a document.
* The change of a calendar item.

The whole list of resources where a notification can be received from can be found [here](https://docs.microsoft.com/en-us/graph/api/resources/webhooks?view=graph-rest-1.0).

Notification of these changes is essential for applications to react properly to these changes. Following are some important considerations in design.

1. Change notifications sent by Graph via webhooks. Many large enterprises block the inbound traffic for security reasons, which limits the ability for the webhook to reach its endpoint.
1. Change notifications have a limit lifetime and needs to be renewed before they expire. Many background business processes that need to subscribe to changes typically run longer than the lifetime of a subscription.
1. The Graph API use throttling to protect itself. This is important for SaaS Applications that can execute large amount of graph calls. The number of calls to Graph API need to be reduced to prevent throttling and impact on the performance. So, using notifications instead of polling and sharing notifications between multiple clients can reduce the number of calls to Graph API.

## Traverse Restricted inbound http traffic

The Microsoft Graph allows third-party applications to subscribe to Office 365 activity through change notifications. When first introduced, change notifications had to be implemented with a webhook accessible by the Microsoft Graph. The network ingress of webhooks made them a non-starter for many organizations. An approach is required where incoming webhooks can traverse inbound network policies that prevent inbound http traffic.

## Subscription lifetime management

A subscription has a limited lifetime and the client that creates that is responsible to renew it before it has expired. This is a complex task where limited solution patterns exist. A re-usable solution or pattern is required that manages and renews subscriptions on behalf of the client.

## Reduce load on Graph and enable real-time notifications

Due to the complexity in managing subscriptions, applications that use graph change notifications often use a polling strategy. Frequent polling can increase load on Graph API substantially. A pub-sub solution will reduce this load and allow multiple applications to use the same notification real-time.

This repo is a solution for managing [Microsoft Graph API Change Notifications](https://docs.microsoft.com/en-us/graph/api/resources/webhooks).
This solution takes out the complexities of managing the subscriptions with MS Graph
and exposes the notifications via SignalR connections.

## Client Applications

A sample Test Client application is included and deployed to the Azure Function and provided as an example.

To run the test client use dotnet serve to host static files. Navigate to
the Test client directory and run:
`dotnet serve -h "Cache-Control: no-cache, no-store, must-revalidate" -p 8080`

See section Testing below for more guidance on testing the Graph Notification Broker.

## Notes

### High Throughput

For high throughput scenarios where the amount of requests per second (rps) to get/create/renew subscriptions begin to get throttled, you can implement an [Async Request Reply Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/async-request-reply). This will add all requests to a queue and then will be processed off of the queue. The processor will send the result back to the client via a push notification with SignalR.

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft
trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
