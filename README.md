# Doge.gg-client

This project is a client for my application from another repository [Doge-gg](https://github.com/DanielSharp01/doge-gg).

It has 2 jobs:
- Reading the League of Legends live client API and relaying it over websocket to the server
- Reading process memory to find an if an in game skillshot (spell that moves in a line) was cast and hit and also relaying that over the same websocket

The project uses asynchronous patterns to allow for event based websocket communication.

The WPF application is just a simple shell around that shows with red and green rectangles whether or not a service is up or down (server, process memory etc.)
