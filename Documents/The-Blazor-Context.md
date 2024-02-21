The Blazor Context

In this article I'll explore in a little depth how Blazor actually works.

Let's assume we have a Blazor Server application running.

When the SPA started and the initial page was built from  `App.razor`, this script was loaded into the browser session.  

```csharp
    <script src="_framework/blazor.web.js"></script>
```  

It downloaded and started the Blazor Server client JS code in the browser session.  This code is resposnsible for establishing and maintaining the *SignalR* session with the Server.

On the server side the Blazor Hub maintains a hub session for each *SignalR* session.

Essentially:

1. Events go from the client to the server.
2. DOM updates [UI changes and event registration/deregistration] go from the server to the client.

The hub session runs a set of services:

1. The Renderer for building and maintaining the Render Tree.
2. A Scoped context in the Services container.
3. A Event queue for UI events posted back from the browser.
4. A Render queue to process render fragments submitted by components.

These all execute code within the Blazor Synchronisation Context.  The synchronisation context runs a *message loop* to service these queues and externally posted code.

It prioritises posted and render work over events.  This ensures that existing work is completed before new work is started.  For example, all the activity around an input control completes before the button click to submit the form starts.

Let's look at clicking the *Increment* button on the *Counter* page.

Step back to when the `Counter` page was rendered.  The DOM updates render the page and add a JS event handler hooked up to the  button.  When clicked, this passes the call into the Blazor JS Client code running in the browser which makes a call back into the Server Hub Session over *SignalR*.

The call crosses the JS/C# boundary when it hits the Hub session.  The Hub session event handler matches up the event to a component and checks to see if the component implements `IHandleEvent`.  If it passes the component event handler to `IHandleEvent.HandleEventAsync` and queues it onto the Event Queue.  If the component doesn't implement `IHandleEvent` it queues the component event handler on to the event queue directly.