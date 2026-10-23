export {};

type JsonObject = Record<string, unknown>;

interface CatchNotification {
  displayName: string;
  profileImageUrl: string | null;
  pokemonName: string;
  isShiny: boolean;
  spriteUrl: string | null;
  cries: {
    latest: string | null;
    legacy: string | null;
  };
}

function object(value: unknown): JsonObject {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    throw new Error("Expected a JSON object.");
  }

  return value as JsonObject;
}

function text(value: unknown): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error("Expected nonempty text.");
  }

  return value;
}

function number(value: unknown, minimum = 0, maximum = 60_000): number {
  if (
    typeof value !== "number" ||
    !Number.isFinite(value) ||
    value < minimum ||
    value > maximum
  ) {
    throw new Error("Invalid numeric overlay setting.");
  }

  return value;
}

function boolean(value: unknown): boolean {
  if (typeof value !== "boolean") {
    throw new Error("Expected a boolean overlay setting.");
  }

  return value;
}

function asset(value: unknown): string | null {
  if (value === null) {
    return null;
  }

  const url = new URL(text(value));

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    throw new Error("Unsupported media URL.");
  }

  return url.href;
}

function element(id: string): HTMLElement {
  const result = document.getElementById(id);

  if (!(result instanceof HTMLElement)) {
    throw new Error(`Missing overlay element: ${id}.`);
  }

  return result;
}

function imageElement(id: string): HTMLImageElement {
  const result = element(id);

  if (!(result instanceof HTMLImageElement)) {
    throw new Error(`Expected an image element: ${id}.`);
  }

  return result;
}

const stage = element("stage");
const notification = element("notification");
const identity = element("identity");
const username = element("username");
const message = element("message");
const avatar = imageElement("avatar");
const sprite = imageElement("sprite");
const spriteRise = element("sprite-rise");
const spriteBob = element("sprite-bob");
const spriteFallback = element("sprite-fallback");
const status = element("status");

const parameters = new URLSearchParams(location.search);
const portrait = parameters.get("layout") === "portrait";
const muted = parameters.get("audio") === "0";

status.hidden = parameters.get("debug") !== "1";

const sleep = (milliseconds: number): Promise<void> =>
  new Promise((resolve) => window.setTimeout(resolve, milliseconds));

function parseNotification(raw: unknown): CatchNotification {
  const value = object(raw);
  const cries = object(value.cries);

  return {
    displayName: text(value.displayName),
    profileImageUrl: asset(value.profileImageUrl),
    pokemonName: text(value.pokemonName),
    isShiny: boolean(value.isShiny),
    spriteUrl: asset(value.spriteUrl),
    cries: {
      latest: asset(cries.latest),
      legacy: asset(cries.legacy),
    },
  };
}

/**
 * Waits briefly for an image before starting the notification.
 * Failure or timeout produces a fallback, never a blocked queue.
 */
function loadImage(
  image: HTMLImageElement,
  url: string | null,
): Promise<boolean> {
  image.removeAttribute("src");

  if (url === null) {
    return Promise.resolve(false);
  }

  return new Promise((resolve) => {
    let settled = false;

    const finish = (loaded: boolean): void => {
      if (settled) return;

      settled = true;
      window.clearTimeout(timer);
      image.onload = null;
      image.onerror = null;

      if (!loaded) {
        image.removeAttribute("src");
      }

      resolve(loaded);
    };

    const timer = window.setTimeout(() => finish(false), 3000);

    image.onload = () => finish(true);
    image.onerror = () => finish(false);
    image.src = url;
  });
}

async function main(): Promise<void> {
  const response = await fetch("/config");

  if (!response.ok) {
    throw new Error("Could not load overlay configuration.");
  }

  const raw: unknown = await response.json();
  const config = object(raw);

  if (config.schemaVersion !== 1) {
    throw new Error("Unsupported overlay schema version.");
  }

  const enabled = boolean(config.enabled);
  const layout = object(config.layout);
  const typography = object(config.text);
  const avatarSettings = object(config.avatar);
  const artwork = object(config.sprite);
  const animation = object(config.animation);
  const audioSettings = object(config.audio);
  const queueSettings = object(config.queue);

  // Read and validate settings once before accepting notifications.
  const settings = {
    canvasWidth: number(layout.canvasWidth, 1),
    canvasHeight: number(layout.canvasHeight, 1),
    x: number(layout.positionX),
    y: number(layout.positionY),
    width: number(layout.width, 1),
    textGap: number(layout.textGapPixels),
    spriteGap: number(layout.spriteGapPixels),
    usernameTemplate: text(typography.usernameTemplate),
    catchTemplate: text(typography.catchTemplate),
    shinyTemplate: text(typography.shinyCatchTemplate),
    usernameColor: text(typography.usernameColor),
    messageColor: text(typography.messageColor),
    shinyColor: text(typography.shinyMessageColor),
    avatarEnabled: boolean(avatarSettings.enabled),
    avatarSize: number(avatarSettings.sizePixels, 1),
    avatarGap: number(avatarSettings.gapPixels),
    spriteSize: number(artwork.sizePixels, 1),
    fallbackLabel: text(artwork.fallbackLabel),
    enter: number(animation.usernameEnterMilliseconds),
    messageDelay: number(animation.messageDelayMilliseconds),
    spriteDelay: number(animation.spriteDelayMilliseconds),
    rise: number(animation.spriteRiseMilliseconds),
    riseDistance: number(animation.spriteRiseDistancePixels),
    hold: number(animation.holdMilliseconds),
    exit: number(animation.exitMilliseconds),
    bobEnabled: boolean(animation.bobEnabled),
    bobDistance: number(animation.bobDistancePixels),
    bobCycle: number(animation.bobCycleMilliseconds, 1),
    audioEnabled: boolean(audioSettings.enabled),
    volume: number(audioSettings.volume, 0, 1),
    maxAudio: number(audioSettings.maxPlaybackMilliseconds),
    preferredCry: text(audioSettings.preferredCry),
    fallbackCry: text(audioSettings.fallbackCry),
    gap: number(queueSettings.gapMilliseconds),
  };

  if (
    !["latest", "legacy"].includes(settings.preferredCry) ||
    !["latest", "legacy"].includes(settings.fallbackCry)
  ) {
    throw new Error("Cry preference must be latest or legacy.");
  }

  notification.style.fontFamily = text(typography.fontFamily);
  notification.style.textShadow =
    `0 2px ${number(typography.shadowBlurPixels)}px ` +
    text(typography.shadowColor);

  username.style.fontSize = `${Math.min(number(typography.usernameFontSizePixels, 1), portrait ? 84 : 500)}px`;

  message.style.fontSize = `${Math.min(number(typography.messageFontSizePixels, 1), portrait ? 64 : 500)}px`;

  identity.style.gap = `${settings.avatarGap}px`;
  avatar.style.width = `${settings.avatarSize}px`;
  avatar.style.height = `${settings.avatarSize}px`;

  message.style.marginTop = `${settings.textGap}px`;
  spriteRise.style.marginTop = `${settings.spriteGap}px`;
  spriteBob.style.width = `${settings.spriteSize}px`;
  spriteBob.style.height = `${settings.spriteSize}px`;

  sprite.style.imageRendering = boolean(artwork.pixelated)
    ? "pixelated"
    : "auto";

  spriteFallback.textContent = settings.fallbackLabel;

  function resize(): void {
    const width = portrait ? 1080 : settings.canvasWidth;
    const height = portrait ? 1920 : settings.canvasHeight;
    const scale = Math.min(innerWidth / width, innerHeight / height);

    stage.style.width = `${width}px`;
    stage.style.height = `${height}px`;
    stage.style.transform = `scale(${scale})`;

    notification.style.left = `${portrait ? width / 2 : settings.x}px`;
    notification.style.top = `${settings.y}px`;
    notification.style.width = `${Math.min(settings.width, width - 80)}px`;
  }

  resize();
  window.addEventListener("resize", resize);

  const queue: CatchNotification[] = [];
  let running = false;

  async function show(catchEvent: CatchNotification): Promise<void> {
    const animations: Animation[] = [];
    let cry: HTMLAudioElement | undefined;
    let audioTimer: number | undefined;

    notification.hidden = true;
    notification.style.opacity = "1";
    identity.style.opacity = "0";
    message.style.opacity = "0";
    spriteRise.style.opacity = "0";

    username.textContent = settings.usernameTemplate.replaceAll(
      "{displayName}",
      catchEvent.displayName,
    );

    message.textContent = (
      catchEvent.isShiny ? settings.shinyTemplate : settings.catchTemplate
    ).replaceAll("{pokemonName}", catchEvent.pokemonName);

    username.style.color = settings.usernameColor;
    message.style.color = catchEvent.isShiny
      ? settings.shinyColor
      : settings.messageColor;

    const [avatarLoaded, spriteLoaded] = await Promise.all([
      loadImage(
        avatar,
        settings.avatarEnabled ? catchEvent.profileImageUrl : null,
      ),
      loadImage(sprite, catchEvent.spriteUrl),
    ]);

    avatar.hidden = !avatarLoaded;
    sprite.hidden = !spriteLoaded;
    spriteFallback.hidden = spriteLoaded;
    sprite.alt = catchEvent.pokemonName;

    const animate = (
      target: HTMLElement,
      frames: Keyframe[],
      duration: number,
    ): Animation => {
      const result = target.animate(frames, {
        duration,
        fill: "forwards",
        easing: "ease-out",
      });

      animations.push(result);
      return result;
    };

    try {
      notification.hidden = false;

      animate(
        identity,
        [
          { opacity: 0, transform: "translateY(20px)" },
          { opacity: 1, transform: "translateY(0)" },
        ],
        settings.enter,
      );

      // Both delays are measured from the notification's start.
      await Promise.all([
        (async () => {
          await sleep(settings.messageDelay);
          animate(message, [{ opacity: 0 }, { opacity: 1 }], settings.enter);
          await sleep(settings.enter);
        })(),
        (async () => {
          await sleep(settings.spriteDelay);

          animate(
            spriteRise,
            [
              {
                opacity: 0,
                transform: `translateY(${settings.riseDistance}px)`,
              },
              { opacity: 1, transform: "translateY(0)" },
            ],
            settings.rise,
          );

          if (settings.audioEnabled && !muted) {
            const preferred =
              settings.preferredCry === "latest"
                ? catchEvent.cries.latest
                : catchEvent.cries.legacy;

            const fallback =
              settings.fallbackCry === "latest"
                ? catchEvent.cries.latest
                : catchEvent.cries.legacy;

            const cryUrl = preferred ?? fallback;

            if (cryUrl !== null) {
              cry = new Audio(cryUrl);
              cry.volume = settings.volume;

              audioTimer = window.setTimeout(
                () => cry?.pause(),
                settings.maxAudio,
              );

              void cry.play().catch((error: unknown) => {
                console.warn(
                  "Cry could not play; continuing notification.",
                  error,
                );
              });
            }
          }

          await sleep(settings.rise);

          if (settings.bobEnabled) {
            animations.push(
              spriteBob.animate(
                [
                  { transform: "translateY(0)" },
                  { transform: `translateY(-${settings.bobDistance}px)` },
                  { transform: "translateY(0)" },
                ],
                {
                  duration: settings.bobCycle,
                  iterations: Infinity,
                  easing: "ease-in-out",
                },
              ),
            );
          }
        })(),
        sleep(settings.enter),
      ]);

      await sleep(settings.hold);
      animate(notification, [{ opacity: 1 }, { opacity: 0 }], settings.exit);
      await sleep(settings.exit);
    } finally {
      notification.hidden = true;
      window.clearTimeout(audioTimer);
      cry?.pause();
      animations.forEach((item) => item.cancel());
    }
  }

  async function drain(): Promise<void> {
    if (running) return;
    running = true;

    try {
      while (queue.length > 0) {
        const next = queue.shift();
        if (next === undefined) break;

        try {
          await show(next);
        } catch (error: unknown) {
          console.error("Notification failed.", error);
        }

        await sleep(settings.gap);
      }
    } finally {
      running = false;
    }
  }

  if (!enabled) {
    status.textContent = "Overlay disabled in configuration.";
    return;
  }

  // EventSource reconnects automatically when the local server returns.
  const events = new EventSource("/events");

  events.onopen = () => {
    status.textContent = "PokéHunter connected";
  };

  events.onerror = () => {
    status.textContent = "PokéHunter disconnected — reconnecting";
  };

  events.onmessage = (event: MessageEvent<string>) => {
    try {
      const parsed: unknown = JSON.parse(event.data);
      queue.push(parseNotification(parsed));
      void drain();
    } catch (error: unknown) {
      console.error("Rejected invalid overlay notification.", error);
    }
  };
}

void main().catch((error: unknown) => {
  console.error(error);
  status.hidden = false;
  status.textContent =
    error instanceof Error ? error.message : "Overlay initialization failed.";
});
