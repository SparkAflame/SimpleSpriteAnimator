/*
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at https://mozilla.org/MPL/2.0/.
*/

using System;
using System.Runtime.CompilerServices;

using UnityEngine;

using Random = UnityEngine.Random;


// ReSharper disable once CheckNamespace

namespace SparkAflame.Sprites
{
   /// <summary>
   /// <para>
   ///   This is a very simple sprite animator.
   /// </para>
   /// <para>
   ///   The rationale for this class is that Unity's Mecanim, Playables, and even the 2D Animation packages are all
   ///   massively over-engineered and have legion shortcomings when all one wants is a simple, script controlled, 2D
   ///   animation of a door opening, or a candle burning!
   /// </para>
   /// <para>
   ///   NOTE 1: As mitigation against undesirable effects Unity limits the maximum time between frames to
   ///   Time.maximumDeltaTime.  This defaults to 0.1s; hence, if the application frame rate falls below 10 FPS the
   ///   application will still believe it is running at 10 FPS!  The consequence of this is that animations may run
   ///   too slowly at very low application frame rates.
   ///   See https://docs.unity3d.com/ScriptReference/Time-maximumDeltaTime.html for more information.
   /// </para>
   /// <para>
   ///   NOTE 2: Playback can be slightly inaccurate if the application frame rate is close to the animation's frame
   ///   rate.  E.g. two animations - one Looped and one PingPongLooped - can get out of step in this situation.
   /// </para>
   /// </summary>
   [RequireComponent( typeof( SpriteRenderer ) )]
   public sealed class SpriteAnimator : MonoBehaviour
   {
      private const float Epsilon        = 0.00001f; // as frame rate = 27h 46m 40s
      private const int   MinSpriteCount = 1;


      public event Action         OnAnimationComplete; // Animation has finished playing.
      public event Action < int > OnNewAnimationFrame; // The animation has advanced to the next frame.


      public enum AnimationModes
      {
         OneShot,        // Play once then stop.
         Looped,         // Play once then return to the beginning to play again.
         PingPong,       // Play forwards then backwards then stop.
         PingPongLooped, // Like PingPong but don't stop.
         Random,         // Play frames in a random order.
      }


      private enum PlayModes
      {
         Stopped, // The animation is stopped.
         Paused,  // The animation is (temporarily) paused and may be resumed..
         Playing, // The animation is playing.
         Inactive,
      }


      private enum InitalStates
      {
         Stopped,
         Playing,
         Inactive,
      }


      #region > > > > >   Script Parameters   < < < < <

      [Tooltip( "The animation frame rate in frames-per-second (FPS)." )]
      [SerializeField]
      private float _frameRate = 12.0f;

      [Tooltip( "The offset from the first frame that the animation should start at." )]
      [SerializeField]
      private int _startFrameOffset;

      [Tooltip( "The animation mode." )]
      [SerializeField]
      private AnimationModes _animationMode;

      [Tooltip(
         "Ignored unless Animation Mode is \"Random\".  If this option is checked then the random frame chooser "
         + "is allowed to pick the same frame that is currently displayed, otherwise it will try harder to pick a "
         + "different frame."
      )]
      [SerializeField]
      private bool _allowDuplicateRandomFrames;

      [Tooltip( "If checked then the animation plays in reverse, otherwise it plays forwards." )]
      [SerializeField]
      private InitalStates _initialState = InitalStates.Stopped;

      [Tooltip(
         "Ignored unless Animation Mode is \"One Shot\".  If checked then when a one-shot animation completes it "
         + "will reset to the first frame then stop, otherwise it will just stop on the last frame."
      )]
      [SerializeField]
      private bool _resetToFirstFrame;

      [Tooltip( "The sprite displayed when the animation is \"inactive\"." )]
      [SerializeField]
      private Sprite _inactiveSprite;

      [Tooltip( "The sprites that comprise the animation." )]
      [SerializeField]
      private Sprite [] _sprites;

      #endregion


      #region > > > > >   Public Accessors   < < < < <

      /// <summary>
      ///   Gets or sets the animation play mode.  The setter does nothing if the animation is already playing or if
      ///   the caller tries to set an invalid value.
      /// </summary>
      public AnimationModes AnimationMode
      {
         get => _animationMode;

         set
         {
            if ( ( _playMode != PlayModes.Playing ) && Enum.IsDefined( typeof( AnimationModes ), value ) )
            {
               _animationMode = value;
            }
         }
      }

      /// <summary>
      ///   Gets or sets the current frame number and, if setting, updates the sprite renderer with the sprite for that
      ///   frame.  The setter does nothing if the new value is negative or exceeds the number of defined sprites.
      ///   Values are zero based.
      /// </summary>
      public int CurrentFrame
      {
         get => _currentFrame;

         set
         {
            if ( ( value >= 0 ) && ( value < _sprites.Length ) )
            {
               _currentFrame = value;

               UpdateSpriteImage();
            }
         }
      }

      /// <summary>
      ///   If <c>false</c> then the animation is playing forwards (i.e. normally); if <c>true</c> then it is playing
      ///   in reverse (i.e. backwards).
      /// </summary>
      public bool IsReversed
      {
         get => _reversed;
         set => _reversed = value;
      }

      /// <summary>
      ///   Gets or sets the animation's visibility.
      /// </summary>
      public bool IsVisible
      {
         get => _spriteRenderer.enabled;
         set => _spriteRenderer.enabled = value;
      }

      #endregion


      private bool CanPlayAnimation
      {
         [MethodImpl( MethodImplOptions.AggressiveInlining )]
         get =>
            _canEnable
            && ( null != _sprites )
            && ( _sprites.Length > MinSpriteCount );
      }


      private SpriteRenderer _spriteRenderer;    // The sprite renderer.
      private float          _frameWaitTime;     // The time between frames = ( _playbackDuration / _sprites.Count ).
      private float          _nextFrameTime;     // The game time when the animation frame should be updated.
      private int            _currentFrame;      // The index of the currently displayed animation frame.
      private bool           _animatingForwards; // The animation direction; true = forwards, false = backwards.
      private PlayModes      _playMode;          // Whether the animation is Playing, Paused, or Stopped.
      private bool           _reversed;          // if true then animation will play backwards when started.
      private bool           _canEnable = true;  // if false then there is no animation to play.


      #region > > > > >   Unity Event Methods   < < < < <

      private void Awake()
      {
         _spriteRenderer = GetComponent < SpriteRenderer >();
         _playMode       = PlayModes.Stopped;
         _reversed       = _frameRate < 0.0f;
         enabled         = false;

         if ( null == _spriteRenderer )
         {
            // Without a sprite renderer this component can do nothing so disable it and log a warning.

            _canEnable = false;

            Debug.LogWarning( $"{nameof( SpriteAnimator )} requires a SpriteRenderer but one was not found; the component has been disabled." );
         }
         else
         {
            if ( ( null == _sprites ) || ( _sprites.Length < 1 ) )
            {
               // No sprites have been assigned, so try using the inactive sprite.  Will become invisible if that too
               // is unassigned.

               _currentFrame          = -1;
               _playMode              = PlayModes.Stopped;
               _spriteRenderer.sprite = _inactiveSprite;
               _canEnable             = false;
            }
            else
            {
               switch ( _initialState )
               {
                  case InitalStates.Stopped :
                     SetInitialFrame();
                     _playMode = PlayModes.Stopped;
                     UpdateSpriteImage();

                     break;

                  case InitalStates.Playing :
                     SetInitialFrame();
                     Play();

                     break;

                  case InitalStates.Inactive :
                     Deactivate();

                     break;

                  default :
                     throw new ArgumentOutOfRangeException();
               }
            }
         }
      }


      private void OnDestroy()
      {
         // Drop all references to event listeners.

         OnAnimationComplete = null;
         OnNewAnimationFrame = null;
      }


      private void Update()
      {
         float currentTime = Time.time;

         if ( ( _playMode != PlayModes.Playing ) || ( currentTime < _nextFrameTime ) )
         {
            return;
         }

         // Calculate how many animation frames we need to advance by

         int frames = 1 + (int) ( ( currentTime - _nextFrameTime ) / _frameWaitTime );

         _nextFrameTime += frames * _frameWaitTime;

         // Update the initial sprite image.

         if ( _animationMode == AnimationModes.Random )
         {
            int counter = 0;

            // If picking the same random frame is not allowed then try up to 10 times to generate a unique frame
            // index.  If that isn't possible then we have no choice but to display the same frame again.

            do
            {
               frames = GetRandomFrameIndex( 0, _sprites.Length );
            }
            while ( !_allowDuplicateRandomFrames && ( frames == _currentFrame ) && ( ++counter < 10 ) );

            _currentFrame = frames;

            UpdateSpriteImage();
         }
         else
         {
            if ( _animatingForwards )
            {
               NextFrameForwards( frames );
            }
            else
            {
               NextFrameBackwards( frames );
            }
         }
      }

      #endregion


      /// <summary>
      ///   Called to stop the animation and notify interested listeners of this fact.
      /// </summary>
      private void AnimationIsComplete()
      {
         Stop();
         OnAnimationComplete?.Invoke();
      }


      /// <summary>
      ///   Sets the animation as inactive.  This stops the animation if it is playing and instead shows the sprite
      ///   assigned as the inactive sprite.
      /// </summary>
      public void Deactivate()
      {
         switch ( _playMode )
         {
            case PlayModes.Stopped :
               break;

            case PlayModes.Paused :
            case PlayModes.Playing :
               Stop();

               break;

            case PlayModes.Inactive :
               return;

            default :
               throw new ArgumentOutOfRangeException();
         }

         _currentFrame          = -1;
         _playMode              = PlayModes.Inactive;
         _spriteRenderer.sprite = _inactiveSprite;
      }


      /// <summary>
      ///   Called when a one-shot animation completes.
      /// </summary>
      /// <param name="lastFrameId">
      ///   The ID of the animation frame to show.
      /// </param>
      private void EndOneShotMode( int lastFrameId )
      {
         CurrentFrame = lastFrameId;

         AnimationIsComplete();
      }


      /// <summary>
      ///   Called when a ping-pong animation completes one half of the animation.
      /// </summary>
      /// <param name="isComplete">
      ///   Whether (<c>true</c>) or not (<c></c>) the animation has completed both the forward and backward halves.
      /// </param>
      /// <param name="lastFrameId">
      ///   The ID of the animation frame to show.
      /// </param>
      private void EndPingPongMode( bool isComplete, int lastFrameId )
      {
         if ( isComplete )
         {
            AnimationIsComplete();
         }
         else
         {
            _animatingForwards = !_animatingForwards;
            CurrentFrame       = lastFrameId;
         }
      }


      /// <summary>
      ///   Gets a random frame index as an integer between <paramref name="lowerBound"/> and
      ///   (<paramref name="upperBound"/> - 1).
      /// </summary>
      /// <param name="lowerBound">The lower bound of the range (inclusive).</param>
      /// <param name="upperBound">The upper bound of the range (exclusive).</param>
      /// <returns></returns>
      private static int GetRandomFrameIndex( int lowerBound, int upperBound )
      {
         return Random.Range( lowerBound, upperBound );
      }


      /// <summary>
      ///   Advances to the next animation frame when playing the animation clips in reverse.
      /// </summary>
      private void NextFrameBackwards( int frames )
      {
         _currentFrame -= frames;

         if ( _currentFrame >= 0 )
         {
            UpdateSpriteImage();
         }
         else
         {
            switch ( AnimationMode )
            {
               case AnimationModes.OneShot :
                  EndOneShotMode( _resetToFirstFrame ? _sprites.Length - 1 : 0 );

                  break;

               case AnimationModes.Looped :
                  CurrentFrame = _sprites.Length - 1;

                  break;

               case AnimationModes.PingPong :
                  EndPingPongMode( !_reversed, Mathf.Max( _currentFrame + 2, 1 ) );

                  break;

               case AnimationModes.PingPongLooped :
                  _animatingForwards = true;
                  CurrentFrame       = Mathf.Max( _currentFrame + 2, 1 );

                  break;

               case AnimationModes.Random :
                  throw new ArgumentOutOfRangeException();

               default :
                  throw new ArgumentOutOfRangeException();
            }
         }
      }


      /// <summary>
      ///   Advances to the next animation frame when playing the animation clips forwards.
      /// </summary>
      private void NextFrameForwards( int frames )
      {
         _currentFrame += frames;

         if ( _currentFrame < _sprites.Length )
         {
            UpdateSpriteImage();
         }
         else
         {
            switch ( AnimationMode )
            {
               case AnimationModes.OneShot :
                  EndOneShotMode( _resetToFirstFrame ? 0 : _sprites.Length - 1 );

                  break;

               case AnimationModes.Looped :
                  CurrentFrame = 0;

                  break;

               case AnimationModes.PingPong :
                  EndPingPongMode( _reversed, Mathf.Min( _currentFrame - 2, _sprites.Length - 2 ) );

                  break;

               case AnimationModes.PingPongLooped :
                  _animatingForwards = false;
                  CurrentFrame       = Mathf.Min( _currentFrame - 2, _sprites.Length - 2 );

                  break;

               case AnimationModes.Random :
                  throw new ArgumentOutOfRangeException();

               default :
                  throw new ArgumentOutOfRangeException();
            }
         }
      }


      /// <summary>
      ///   Pauses a playing animation.
      /// </summary>
      public void Pause()
      {
         if ( _playMode == PlayModes.Playing )
         {
            _playMode = PlayModes.Paused;
            enabled   = false;
         }
      }


      /// <summary>
      ///   Plays or restarts from the beginning the animation using the currently configured settings.  Does nothing
      ///   if: (1) an animation is already playing; or (2) less than two sprites are assigned.
      /// </summary>
      public void Play()
      {
         Play( _startFrameOffset );
      }


      /// <summary>
      ///   Plays or restarts from the beginning the animation using the currently configured settings.  Does nothing
      ///   if: (1) an animation is already playing; or (2) less than two sprites are assigned.
      /// </summary>
      /// <param name="frameOffset">
      ///   The offset (from the first frame) to start the animation from.
      /// </param>
      public void Play( int frameOffset )
      {
         if ( ( _playMode != PlayModes.Playing ) && CanPlayAnimation )
         {
            float frameRate = Mathf.Abs( _frameRate );
            int   offset    = ( frameOffset >= 0 ) && ( frameOffset < _sprites.Length ) ? frameOffset : 0;

            _animatingForwards = !_reversed;
            _frameWaitTime     = 1.0f / frameRate;
            CurrentFrame       = _animatingForwards ? offset : _sprites.Length - offset - 1;

            // Don't actually enable play mode unless the frame rate is high enough.

            if ( frameRate > Epsilon )
            {
               ResumeInternal();
            }
         }
      }


      /// <summary>
      ///   Resumes a paused animation from the point it was paused.
      /// </summary>
      public void Resume()
      {
         if ( ( _playMode == PlayModes.Paused ) && CanPlayAnimation )
         {
            // Don't actually enable play mode unless the frame rate is high enough.

            float frameRate = Mathf.Abs( _frameRate );

            if ( frameRate > Epsilon )
            {
               ResumeInternal();
            }
         }
      }


      [MethodImpl( MethodImplOptions.AggressiveInlining )]
      private void ResumeInternal()
      {
         _nextFrameTime = Time.time + _frameWaitTime;
         _playMode      = PlayModes.Playing;
         enabled        = true;
      }


      [MethodImpl( MethodImplOptions.AggressiveInlining )]
      private void SetInitialFrame()
      {
         CurrentFrame = _reversed ? _sprites.Length - 1 : 0;
      }


      /// <summary>
      ///   Stops a playing animation.
      /// </summary>
      public void Stop()
      {
         _playMode = PlayModes.Stopped;
         enabled   = false;
      }


      /// <summary>
      ///   Updates the sprite renderer with the next sprite that is to be displayed.
      /// </summary>
      [MethodImpl( MethodImplOptions.AggressiveInlining )]
      private void UpdateSpriteImage()
      {
         _spriteRenderer.sprite = _sprites [ _currentFrame ];

         OnNewAnimationFrame?.Invoke( _currentFrame );
      }
   }
}
