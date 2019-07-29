# AnimationPlayer
An animation player for Unity, to replace the Animator/AnimatorController.

Very much WIP! I'm developing this to use in a real Unity game, to ease the workload for implementing animations in that game. 

This means that this isn't in any way stable! Use at your own caution, don't expect upgrades to work yet.

## Installation

Minimum required Unity version is 2018.4
Add this line to Packages/manifest.json:
"com.baste.animationplayer": "https://github.com/Baste-RainGames/AnimationPlayer.git"

See this forum thread on the Unity forums for info about getting packages from urls: https://forum.unity.com/threads/git-support-on-package-manager.573673/

If you want to make changes to the projects (to fork or for pull requests), you would clone this project into some other Unity project's Packages folder. If that project is already under project control with git, you'd instead add it as a submodule in the same location.

## Core idea

The AnimationPlayer is a Unity Component which is used to play animations. Like the built-in Animator Controller, you define both which animations states exist, and how to transition between them. 

Unlike the AnimatorController, the AnimationPlayer does not listen for variables to trigger transitions between states. Instead, you tell it to play a state. The AnimationPlayer checks if there's a transition defined from the current state to the state you told it to play. If a transition is defined, it's used. If not, a (user-defined) default transition is used.

This both makes the API a lot simpler, and makes the player a lot less bug prone. When you call Play("Attack"), you're guaranteed that the animation player will start transitioning to "Attack" right away.

Both animators and the programmers should at all times feel like they know what's going on. 

## Goals (long term)
- Completely open source always! If you need something right away, you don't have to send a feature request to Unity and wait for it to never get implemented!

- The AnimatonPlayer will have the full set of features from the AnimatorController. This means that it will contain
  - 1D and 2D blend trees
  - Transitions that are definable and previewable in the editor
  - IK for Humanoid (and Generic if it's ever implemented by Unity)
  - Layers (both additive and override, and with masks)
  - Animation Events!

- New features
  - Animation Sequences are several clips strung together as a single state, either looping on the last clip or looping through all of the clips.
  - Animation Selections allow you to select randomly from a set of clips when a state is played. 
  - Transition-by-clip are transitions containing a clip which you blend through. Usefull for tuning a transition without having to introduce an entire state for the job.
  - Metadata view to quickly check which clips and models are used by the animation player.
  - Features to mass-replace clips in cases where you're replacing the bone structure of a model, or something similar.
  - Animation events live in the states in the Animation Player, rather than on the clips. This makes it much easier to work with the events. You also don't have to wait for a reimport of the entire .fbx model whenever you want to move the timing of an event.
  - Several possible transitions between the same states will be available. In that case, you'll name the transitions: Play("state name", "transition name")
  - Non-linear transitions. It will be possible to eg. ease into a new state. This will reduce the need for custom-made transition states.
  - You can add and remove new states and transitions at runtime. There's not a strict seperation between the edit time and runtime representation of your animation data.

- Simple to use
  - If you just want to play a single animation on a single object, you can simply drag the animation into the component, and you're done. 
  - Additional features should not be in the way unless you need them. 
  - Clear seperation of concerns - the coders care about gettings states to play, the animator cares about how the animation and transitions look.
  - You can always play an animation, and get a reasonable transition from your current animation, no matter what state the animation is currently in.

- Comfortable to use API:
  - The concept of using Triggers, Bools, Floats and Ints to control an animation graph is gone!
  - To play an animation named "Attack", call animationPlayer.Play("Attack"). This will transition to that animation using the transition rules set up in the editor.
  - To get the name of the currently playing animation, call animationPlayer.GetPlayingState().Name;
  - To get the length of the currently playing animation, call animationPlayer.GetPlayingState().Duration;
  - If you want detailed information on what's going on, like if there's a transition, or what the state of the current blending between state is, that information is available.
  - Generally, all information you'll want will be available. There shouldn't be anything that "we haven't exposed".
  - Methods that doesn't take half the screen. No more GetCurrentAnimatorStateInfo(0).IsName("SomeState"). It's just IsPlaying("SomeState").

- Comfortable to use for non-technical Animators
  - Both states and transitions will have views that are intuitive to use, with real-time previews. 
  - The responsibility of deciding which gameplay events that triggers a transition is moved from the animation player to the scripts, so the animator only has to care about what an animation or transition looks like
  - Adding animations to a player is super-easy; just drag an fbx model or animation clip into the player's inspector, and you get a convenient menu to add states.
  - Optional complexity in state types. If your animator wants an animation state to have an intro before starting to loop, they can turn the animation into a "Sequence", without that impacting the code.

- Easy to understand
  - The animationPlayer only considers a single state as the currently played state, and immediately sets that when you change state. So if you call Play("Attack"), attack is the currently played state. On the same frame, even!
  - As few as possible gotchas when it comes to ordering of things.
  - Both the editor side and the API will be well-documented, and examples for both will be included.
  - Tools that graphs the AnimationPlayer's state over time, and visualizes what messages were received at what times, and how the player reacted to those messages. This will make it much easier to hunt down bugs, and to understand what's going on.

- Be at least as fast as playing animations through an AnimatorController. Preferably a lot faster
  - Avoid as many as possible performance gotchas. If there's an AnimationPlayer attached to a GameObject that's not currently playing any animations, that shouldn't have any overhead! 

## Contributions
Yes, please!
If you have a bug fix, or have a feature, I'd be happy to take pull requests! If you submit one, It'd be great if you could try to at least vaguely follow the code style (four spaces for indents, brackets on the next line)  

Before you start working on something, please check Assets/Docs/ATTODO.txt, which contains a more comprehensive list of tasks I'm working on. If the thing you want to do is listed there, I might already have finished it, but not moved it over from the game I'm working on to this repository.
Also, if you have an idea for a feature that's not listed under Goals above or in ATTODO, it's probably a good idea to create an issue on Github to ask if it's something I think fits the project.

## Motivation, aka. "Why I think you need to replace the AnimatorController in the first place."
See Docs/Motivation.txt if you care
