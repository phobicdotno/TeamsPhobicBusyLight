const int RELAY_PIN = 9;

void setup() {
  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, LOW);
  // Turn off both onboard LEDs initially
  TXLED1;  // TX LED off (active low: 1=off)
  RXLED1;  // RX LED off (active low: 1=off)
  Serial.begin(9600);
}

void loop() {
  if (Serial.available() > 0) {
    char cmd = Serial.read();
    if (cmd == '1') {
      // Busy: relay ON, TX LED ON (red), RX LED OFF
      digitalWrite(RELAY_PIN, HIGH);
      TXLED0;  // TX LED on
      RXLED1;  // RX LED off
    } else if (cmd == '0') {
      // Available: relay OFF, TX LED OFF, RX LED ON (green)
      digitalWrite(RELAY_PIN, LOW);
      TXLED1;  // TX LED off
      RXLED0;  // RX LED on
    } else if (cmd == 'X') {
      // Disconnected/off: relay OFF, both LEDs OFF
      digitalWrite(RELAY_PIN, LOW);
      TXLED1;  // TX LED off
      RXLED1;  // RX LED off
    }
  }
}
