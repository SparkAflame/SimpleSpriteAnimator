# Simple Sprite Animator

This, as it's name suggests, is a very simple 2D sprite animator for Unity 2020.3
and later.  It _should_ work with earlier Unity versions but is untested.

It is designed to be used in those situations where to use Unity's Mecanim,
Playables, or 2D Animation package would be too burdensome - such as when prototyping
or animating simple static interactable items - or when an effect is more difficult
to achieve than it really should be (e.g. rewinding and replaying an animation in
Mecanim).

I use this to animate floor and wall switches, opening doors, burning candles,
chandeliers and other similar effects that occur due to a player's or non-player
character's actions.

I _don't_ use this to animate the player's character's movement or combat
animations or for effects such as platforms that fall away when walked on.

## Licence

Mozilla Public License, v. 2.0. If a copy of the MPL was not distributed with this
file, You can obtain one at https://mozilla.org/MPL/2.0/.

See the file named "LICENSE" in this repository.

All trademarks acknowledged.

## Features

* Five animation modes:
  * One-shot : play the animation once and stop.
  * Looped : like one-shot but start the animation again from the beginning when it
    ends.
  * Ping-pong : play the animation forwards, then backwards, then stop.
  * Ping-pong Looped : like ping-pong but don't stop.
  * Random : play the animation continuously picking the next frame at random.
* Play automatically at startup, manually under script control, or both.
* Play animations forwards or backwards.
* Start an animation from the beginning or from any arbitrary frame.
* Can (re-)start, pause, resume, and stop animations.
* Can set a sprite that is only shown when the animation is `Inactive`.  This is
  is a separate state to `Paused` and `Stopped` and is intended to be used to
  indicate that the object is not usable, not activated, or in some similar state.
  Examples might include a locked door, an unlit candle, a jammed switch, etc.
* Uses Unity's Sprite Renderer to draw the sprites so should work on any render
  pipeline supported by the Sprite Renderer.
* No GC allocations.
* To save resources the script disables itself at startup and enables itself only
  when actually playing an animation.
* Raises C# events when:
  * A new frame is displayed.
  * The animation ends (one-shot and ping-pong only).

## Installing

Use the Unity Package Manager "Add package from git URL" to install into your
project.

Alternatively you can clone this repository directly into your project's Packages
folder.

## Using

For the most basic animation:

1. Create a sprite (2D Object => Sprites => Square) or empty game object in your
   scene.
2. On this object click "Add Component" and select the "Sprite Animator" script.
   A Sprite Renderer component will be automatically added if one is not already
   present.
3. In the Sprite Animator component assign your sprites in the order in which they
   should play.  For convenience one can multi-select the sprites in the project 
   view and drag and drop them onto the `Sprites` list.
4. Update the `Sprite Renderer`'s `Sprite` to the first of the animation sequence.
   This is purely cosmetic - it will be overridden when the Sprite Animator
   starts - but is necessary if you wish to see precisely where in the scene you
   are placing the animation.
5. (Optional) Set the `Inactive Sprite`.  This is only necessary if your animation
   can be set as `Inactive` (see below) and you wish to show a specific sprite
   when inactive.
6. Enter the desired frame rate.
7. Set the initial state as desired.
8. Choose your animation mode.
9. Set other options as required.

Once you have the basic animation working you can add any other components -
colliders, triggers, C# scripts, etc. - as you see fit.

## Public API

### C# Events

Name                | Description
----                | ----
OnAnimationComplete | Raised when an animation finishes - one-shot and ping-pong only.  Never raised when the animation mode is `Looped`, `Ping Pong Looped`, or `Random`.
OnNewAnimationFrame | When a new animation frame is displayed.

> **NOTE:** Take care when using C# events; they can cause memory leaks and / or 
> excessive GC allocations if not used properly.

### Inspector Properties

To prevent these being set arbitrarily to values that are nonsensical given the
current animation state these are all declared `private`.  They can only have their
values altered through the provided C# properties and methods.


Name                          | Type      | Description
----                          | ----      | ---
Frame Rate                    | float     | The rate in frames-per-second at which animation is played.  If positive then the animation plays forwards; if negative it plays backwards; if zero it doesn't play at all.
Start Frame Offset            | int       | The start frame as an offset from the animation's first frame.  E.g. assuming the animation has 20 frames and start frame offset is 5 then if playing forwards the animation would start at the sixth frame; if playing backwards it would start at frame 15.  Invalid values - negative or >= sprite count - are silently ignored and zero used instead.
Animation Mode                | enum      | How the animation is played.  Options are `One Shot`, `Looped`, `Ping Pong`, `Ping Pong Looped`, and `Random`
Allow Duplicate Random Frames | bool      | Ignored unless `Animation Mode` is `Random`.  If unchecked then `SpriteAnimator` will try to not play the same animation frame twice in succession.  If checked then the same frame may play two or more times successively.  Depending on the situation the animation may appear to "stutter" when this option is checked. 
Initial State                 | enum      | The initial play state.  The options are `Stopped`, `Playing`, and `Inactive`.
Reset To First Frame          | bool      | If checked then when a `One Shot` or `Ping Pong` animation completes `SpriteAnimator` will re-show the first frame.  If unchecked then the animation will stop on the last frame.
Inactive Sprite               | Sprite    | Optional.  The sprite image that will be displayed when the animation play mode is set to `Inactive`.  This state can be set either by setting `Initial State` to `Inactive` or by calling `Deactivate()` from another script.  **NOTE** that the sprite will simply become invisible if the animation is made `Inactive` when `Inactive Sprite` is unassigned.
Sprites                       | Sprite [] | The sprites that comprise the animation.  Sprites are played in the order they are listed here unless playing the animation backwards when they will be played in the reverse order of their listing.

### C# Properties

Name          | Type | Description
----          | ---- | ----
AnimationMode | enum | Gets or sets the animation mode.  Attempts to set the mode are silently ignored if the animation is playing or an invalid value is given.
CurrentFrame  | int  | Gets or sets the zero based index of the current animation frame.  Attempts to set and invalid index (i.e. negative or >= number of animation frames) are silently ignored.
IsReversed    | bool | Gets or sets whether the animation plays forwards (IsReversed = `false`) or backwards.  Setting this while an animation is playing will have no effect until the animation is stopped and restarted.
IsVisible     | bool | Gets or sets the sprite's visibility.  Colliders and triggers are not disabled if the sprite is made invisible.

### Methods

Name         | Description
----         | ----
Deactivate   | Sets the playback mode to `Inactive`.  Stops the animation if it is `Playing` or `Paused`.  An animation can only be restarted from the beginning once it has been made `Inactive`; it cannot be restarted.
Pause        | Pauses animation playback.
Play         | Starts or restarts the animation from the beginning.
Play( int )  | Starts or restarts the animation from the specified offset.
Resume       | Restarts a paused animation from the point it was paused.
Stop         | Stops animation playback.  An animation will restart from the beginning if `Play()` is subsequently called.
