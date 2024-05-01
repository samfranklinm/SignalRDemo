import React, { useState, useEffect } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import Button from 'react-bootstrap/Button';

function App() {
  const [sentenceFromWebApi, setSentenceFromWebApi] = useState("");
  const [sentenceFromPlugin, setSentenceFromPlugin] = useState("");
  const [streamCancelledMessage, setStreamCancelledMessage] = useState(false); 
  const [connection, setConnection] = useState(null);
  const [streaming, setStreaming] = useState(false);

  useEffect(() => {
    const connect = new HubConnectionBuilder()
      .withUrl("https://localhost:7205/sentenceHub") // this is my webapi url + route to my signalr hub that I set up in my webapi
      .withAutomaticReconnect()
      .build();

    const onReceiveWordFromWebApi = (word) => {
      setSentenceFromWebApi(prev => prev + word + " ");
    };

    const onReceiveWordFromPlugin = (word) => {
      setSentenceFromPlugin(prev => prev + word + " ");
    };

    const onStreamCancelled = (word) => {
      setStreamCancelledMessage(prev => prev + word + " ");
    }

    connect.start().then(() => {
      setConnection(connect);
      connect.on("ReceiveFromWebApi", onReceiveWordFromWebApi); // listener for the event that the hub will send from the webapi
      connect.on("ReceiveFromPlugin", onReceiveWordFromPlugin); // listener for the event that the hub will send from the plugin
      connect.on("StreamCancelled", onStreamCancelled); 
    }).catch(err => console.error('Connection failed: ', err));

    // Clean up on dismount
    return () => {
      if (connect) { 
        connect.off("ReceiveFromWebApi", onReceiveWordFromWebApi); 
        connect.off("ReceiveFromPlugin", onReceiveWordFromPlugin);
        connect.off("StreamCancelled", onStreamCancelled);
        connect.stop(); 
      }
    };
  }, []);

  const getSentenceFromWebApi = () => {
    if (connection) {
      setStreaming(true);
      connection.invoke("StreamToClientFromWebApi")
        .catch(err => {
          console.error('Error invoking the method on the hub: ', err);
          setStreaming(false);
        });
    }
  };

  const getSentenceFromPlugin = () => {
    if (connection) {
      setStreaming(true);
      connection.invoke("StreamToClientFromPlugin")
        .catch(err => {
          console.error('Error invoking the method on the hub: ', err);
          setStreaming(false);
        });
    }
  };

  const cancelStreaming = () => {
    if (connection) {
      setStreaming(false); 
      connection.invoke("CancelStreaming")
          .then(() => {
              console.log("Stream cancellation confirmed by server.");
              setStreamCancelledMessage("Streaming has been cancelled."); 
          })
          .catch(err => {
              console.error('Error invoking CancelStreaming on the hub: ', err);
              setStreaming(true);
          });
    }
  };
  

  const clearSentences = () => {
    setSentenceFromWebApi('');
    setSentenceFromPlugin('');
    setStreamCancelledMessage('');
    setStreaming(false);
};


  return (
    <div className="App">
      <header className="App-header">
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100vh' }}>
          <Button variant="success" style={{ margin: '10px' }} onClick={getSentenceFromWebApi} >
            Stream From WebApi
          </Button>
          <p style={{ color: '#4CAF50', fontSize: '18px' }}>{sentenceFromWebApi}</p>

          <div style={{display: "flex", alignContent: "center", justifyContent: "center"}}>
            <Button variant="primary" style={{ margin: '10px' }} onClick={() => getSentenceFromPlugin()} >
              Stream From Plugin
            </Button>
            <Button variant="danger" style={{ margin: '5px' }} onClick={() => cancelStreaming()}>
              Stop Stream
            </Button>
          </div>
          <p style={{ color: '#008CBA', fontSize: '18px' }}>{sentenceFromPlugin}</p>
          <p style={{ color: '#008CBA', fontSize: '18px' }}>{streamCancelledMessage}</p>
          

          <Button variant="danger" style={{ margin: '10px' }} onClick={clearSentences}>
            Clear Streams
          </Button>
        </div>
      </header>
    </div>
  );
}

export default App;
