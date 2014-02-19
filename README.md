# Windows Azure Mobile Services 

With Windows Azure Mobile Services you can add a scalable backend to your connected client applications in minutes. To learn more, visit our [Developer Center](http://www.windowsazure.com/en-us/develop/mobile).

## Requirements

This solution only works with the Optimistic Concurrency table format. For more info visit http://blogs.msdn.com/b/azuremobile/archive/2013/11/25/what-s-new-in-azure-mobile-services-1-6-4247.aspx.

##Offline Data and Sync

This branch contains a prototype extension to do caching and synchronization for offline scenarios built by @jlaanstra. It currently has the following features:

- only works for WP8, Win8+ and .NET 4.5.
- Cache json responses from tables into a local SQLite database.
- Store local changes and sync them as soon as the network is available.
- Interpret queries and send them to the local SQLite database.
- Only retrieve incremental changes from the server since the last request was made.

*This is a prototype. The above features may contain bugs. If you come across a bug, please open an issues so it can be fixed.*

## Getting Started

To use Offline Data and Sync in your Mobile Service there are a couple things you need to do:

- Update the database schema (see altertable.sql in sql folder).
- Update server scripts (see scripts in scripts folder).
- Add shared scripts and modify package.json to include the node 'async' package.
- Add the `CacheHandler` to the `MobileServiceClient`.
- Choose a `CacheProvider`.
- Think about your business logic if all your assumptions are still valid in offline scenario's and make changes if necessary.
- The Portable library for SQLite (https://sqlitepcl.codeplex.com/) currently has bugs. You have to compile it manually and include th fix from this PR https://sqlitepcl.codeplex.com/SourceControl/network/forks/jlaanstra/sqlitepcl/contribution/6230.

## Important differences

If you use mobile services without this solution and you use the mobile services system properties, these will not be assigned until an item is synchronized with the service.
Request may take significantly longer the first time a request is made or after a lot of offline changes have been made.

## Work in progress

- Ability to find out if a request came from the server or from the local data store.
- Documentation.



 




