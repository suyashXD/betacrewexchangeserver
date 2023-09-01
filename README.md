# betacrewexchangeserver

This repository contains code for a Betacrew Exchange Server and a C# client for connecting to it. The server simulates a stock exchange, and the client retrieves stock ticker data from the server.

## Prerequisites

Before you begin, ensure you have met the following requirements:
Note: This project was developed and tested on a Windows-based environment.

- Node.js (v16.17.0 or higher)
- .NET Core SDK (v7.0.400)
- Git

## Installation

### 1. Clone the repository
git clone https://github.com/suyashxd/betacrewexchangeserver.git

### 2. Navigate to the project directory
cd betacrewexchangeserver

### 3. Start the Betacrew Exchange Server
cd betacrew_exchange_server
node main.js

### 4. Open a new terminal window (keep the server running in the previous terminal)
cd ../client

### 5. Build and run the C# client
dotnet run

### 6. To stop the server, simply press Ctrl + C in the terminal where the server is running

## Result

A json file called processed_data.json will be generated in the client directory that will contain an array of objects, where each object represents a packet of data with increasing sequences

## Note

The code overwrites the existing JSON file instead of creating a new one each time dotnet run is executed.


