// BlackJack Buttler Visual Canvas (Debug only)
//
// This file is copied next to the Debug plugin assembly and reloaded for every
// requested visual frame. `render` must return true before BJB shows the newly
// recorded canvas. Returning false, throwing, or timing out keeps the last
// valid canvas on screen.
//
// The currently exposed context is deliberately small:
//   ctx.clearRect(x, y, width, height)
//   ctx.fillRect(x, y, width, height)
//   ctx.strokeRect(x, y, width, height)
//   ctx.fillText(text, x, y)
//   ctx.fillStyle = "#RRGGBB" | "#RRGGBBAA" | "rgb(...)" | "rgba(...)"
//   ctx.strokeStyle = same colours as fillStyle
//   ctx.lineWidth = number
//   ctx.font = "16px ..."   // retained for the future text renderer
//
// The callback stubs document the future reactive input contract. They are not
// dispatched by this first render-only prototype yet.
function onMouseDown(event) {}
function onMouseUp(event) {}
function onMouseMove(event) {}
function onMouseWheel(event) {}

/*
Example payload (all values are illustrative):

{
  "canvas": { "width": 1080, "height": 640 },
  "phase": "PlayersTurn",
  "recognitionActive": true,
  "dealer": {
    "id": "dealer", "name": "Dealer", "alias": "", "displayName": "Dealer",
    "active": true, "onHold": false, "onBench": false, "currentTurn": false,
    "bank": 0, "bet": 0, "currentHandIndex": 0,
    "hands": [{
      "index": 0, "bet": 0, "points": 17, "stand": false, "bust": false,
      "blackjack": false, "charlie": false, "doubleDown": false, "tripleDown": false,
      "cards": [
        { "value": 10, "label": "10", "suit": "Spades", "symbol": "♠" },
        { "value": 7, "label": "7", "suit": "Hearts", "symbol": "♥" }
      ]
    }]
  },
  "players": [{
    "id": "Alice Example_74", "name": "Alice Example", "alias": "Ace",
    "displayName": "Ace", "worldId": 74, "vip": false, "active": true,
    "onHold": false, "onBench": false, "currentTurn": true, "imaginary": false,
    "bank": 100000, "bet": 5000, "currentHandIndex": 0,
    "hands": [{
      "index": 0, "bet": 5000, "points": 16, "stand": false, "bust": false,
      "blackjack": false, "charlie": false, "doubleDown": false, "tripleDown": false,
      "cards": [
        { "value": 10, "label": "10", "suit": "Clubs", "symbol": "♣" },
        { "value": 6, "label": "6", "suit": "Diamonds", "symbol": "♦" }
      ]
    }]
  }]
}
*/

function render(payload, ctx) {
  // Intentionally blank starter script. Add drawing commands here and return
  // true only when the complete frame has been recorded.
  return true;
}
