const http = require('http');
const WebSocket = require('ws');

const PORT = process.env.PORT || 8080;

// Map<roomId, Map<userId, { ws, x, y, z, yaw }>>
const rooms = new Map();

// HTTP server so Render's health checks pass
const server = http.createServer((_req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/plain' });
  res.end('OK');
});

const wss = new WebSocket.Server({ server });

wss.on('connection', (ws) => {
  let roomId = null;
  let userId = null;

  ws.on('message', (data) => {
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch {
      return;
    }

    if (msg.type === 'join') {
      if (!msg.lobbyId || !msg.userId) return;

      roomId = String(msg.lobbyId);
      userId = String(msg.userId);

      if (!rooms.has(roomId)) rooms.set(roomId, new Map());
      const room = rooms.get(roomId);

      // Send current players to the new joiner
      const players = [];
      for (const [uid, p] of room) {
        players.push({ userId: uid, x: p.x, y: p.y, z: p.z, yaw: p.yaw });
      }
      ws.send(JSON.stringify({ type: 'welcome', players }));

      // Add the new player with a default position
      room.set(userId, { ws, x: 0, y: 0, z: 0, yaw: 0 });

      // Notify everyone else
      broadcast(roomId, userId, { type: 'joined', userId });

      console.log(`[${roomId}] ${userId} joined (${room.size} in room)`);

    } else if (msg.type === 'position') {
      if (!roomId || !userId) return;
      const room = rooms.get(roomId);
      if (!room) return;

      const { x = 0, y = 0, z = 0, yaw = 0 } = msg;

      const player = room.get(userId);
      if (player) Object.assign(player, { x, y, z, yaw });

      broadcast(roomId, userId, { type: 'position', userId, x, y, z, yaw });
    }
  });

  ws.on('close', () => {
    if (!roomId || !userId) return;
    const room = rooms.get(roomId);
    if (!room) return;

    room.delete(userId);
    console.log(`[${roomId}] ${userId} left (${room.size} remaining)`);

    if (room.size === 0) {
      rooms.delete(roomId);
      console.log(`[${roomId}] Empty room removed`);
    } else {
      broadcast(roomId, userId, { type: 'left', userId });
    }
  });

  ws.on('error', (err) => {
    console.error(`WS error [${userId}@${roomId}]: ${err.message}`);
  });
});

function broadcast(roomId, senderId, message) {
  const room = rooms.get(roomId);
  if (!room) return;
  const json = JSON.stringify(message);
  for (const [uid, player] of room) {
    if (uid !== senderId && player.ws.readyState === WebSocket.OPEN) {
      player.ws.send(json);
    }
  }
}

server.listen(PORT, () => {
  console.log(`Position sync server listening on port ${PORT}`);
});
