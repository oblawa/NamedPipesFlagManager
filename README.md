# NamedPipesFlagManager

## Overview
This project provides an inter-process communication (IPC) solution using Windows Named Pipes. It implements a client-server architecture with support for:
- Flag value management (get/set/change/remove)
- Subscription to flag change notifications
- Asynchronous message processing
- Thread-safe operations

Architecture
- Server
  - Manages named pipe instances
  - Maintains flag state (ConcurrentDictionary)
  - Handles client subscriptions
- Client
  - Connects to server pipe
  - Sends commands and receives responses
  - Processes notifications
## Features
- Server-side
  - Concurrent flag management
  - Client connection handling
  - Subscription system for flag changes
  - Message format validation
  - Error handling
- Client-side
  - Thread-safe request/response pattern
  - Asynchronous operations with timeouts
  - Subscription management
  - Notification callbacks

## Installation
1. Clone the repository:
```git clone https://github.com/your-repo/named-pipes-ipc.git```
2. Add the project to your solution
3. Reference NamedPipeProg.NP namespace

## Usage
1. Server Setup
```csharp
var serverService = new NamedPipesServerService();
var serverPipe = serverService.CreateServer();
```
2. Client Operations
```csharp
var clientService = new NamedPipesClientService();
var client = clientService.CreateClient();

// Set flag value
bool success = await client.SetFlag("flag1", 1);

// Get flag value
byte? value = await client.GetFlagValue("flag1");

// Subscribe to changes
await client.SubscribeFlag("flag1", (flagName, newValue) => 
{
    Console.WriteLine($"{flagName} changed to {newValue}");
});

// Change flag value
await client.ChangeFlag("flag1", 2);
```
## Message Protocol
- Command Format
```COMMAND:FLAG_NAME[:VALUE]```
- Response Format
```STATUS:COMMAND:FLAG_NAME[:ADDITIONAL_INFO]```
- Notification Format
``` NOTIFY:FLAG_NAME:NEW_VALUE```
## Status Codes
<table>
    <tr>
        <th>Code</th>
        <th>Description</th>
    </tr>
    <tr>
        <td>SUCCESS</td>
        <td>Operation completed successfully</td>
    </tr>
    <tr>
        <td>ERROR </td>
        <td>Operation failed</td>
    </tr>
    <tr>
        <td>NOTIFY</td>
        <td>Flag value changed notification</td>
    </tr>
</table>

## Limitations
- Windows-only (Named Pipes are Windows-specific)
- Single-machine communication only
- Message size limited by pipe buffer

## Dependencies
- .NET 5.0 or later
- Windows OS
