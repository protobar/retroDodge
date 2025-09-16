# Connection Scene Setup Guide

## Overview
This guide will help you set up the Connection scene with all the necessary UI panels and components for PlayFab authentication.

## Scene Setup Steps

### 1. Create New Scene
1. Right-click in Project window → Create → Scene
2. Name it "Connection"
3. Save it in Assets/Scenes/

### 2. Scene Hierarchy Setup

Create this hierarchy structure:

```
Connection (Scene)
├── Canvas
│   ├── MainPanel
│   │   ├── Background (Image)
│   │   ├── Title (TextMeshPro - "Retro Dodge Rumble")
│   │   ├── SignInButton (Button)
│   │   ├── SignUpButton (Button)
│   │   └── GuestButton (Button)
│   │
│   ├── SignInPanel
│   │   ├── Background (Image)
│   │   ├── Title (TextMeshPro - "Sign In")
│   │   ├── EmailInput (TMP_InputField)
│   │   ├── PasswordInput (TMP_InputField)
│   │   ├── LoginButton (Button)
│   │   ├── BackButton (Button)
│   │   └── ErrorText (TextMeshPro - Red color)
│   │
│   ├── SignUpPanel
│   │   ├── Background (Image)
│   │   ├── Title (TextMeshPro - "Create Account")
│   │   ├── DisplayNameInput (TMP_InputField)
│   │   ├── EmailInput (TMP_InputField)
│   │   ├── PasswordInput (TMP_InputField)
│   │   ├── ConfirmPasswordInput (TMP_InputField)
│   │   ├── RegisterButton (Button)
│   │   ├── BackButton (Button)
│   │   └── ErrorText (TextMeshPro - Red color)
│   │
│   ├── LoadingPanel
│   │   ├── Background (Image)
│   │   ├── LoadingSpinner (Image - rotating sprite)
│   │   └── StatusText (TextMeshPro)
│   │
│   └── ErrorPanel
│       ├── Background (Image)
│       ├── ErrorMessageText (TextMeshPro - Red color)
│       └── CloseButton (Button)
│
├── EventSystem
├── ConnectionManager (Empty GameObject)
└── PlayFabAuthManager (Empty GameObject)
```

### 3. Component Setup

#### ConnectionManager GameObject
- Add `ConnectionManager` script
- Add `PhotonView` component

#### PlayFabAuthManager GameObject
- Add `PlayFabAuthManager` script

#### Canvas Setup
- Add `ConnectionUI` script
- Set Canvas Scaler to "Scale With Screen Size"
- Reference Resolution: 1920x1080

### 4. UI Component Configuration

#### MainPanel
- **SignInButton**: Text = "Sign In"
- **SignUpButton**: Text = "Sign Up"
- **GuestButton**: Text = "Play as Guest"

#### SignInPanel
- **EmailInput**: 
  - Placeholder = "Enter your email"
  - Content Type = Email Address
- **PasswordInput**: 
  - Placeholder = "Enter your password"
  - Content Type = Password
- **LoginButton**: Text = "Sign In"
- **BackButton**: Text = "Back"
- **ErrorText**: 
  - Color = Red
  - Initially hidden (SetActive = false)

#### SignUpPanel
- **DisplayNameInput**: 
  - Placeholder = "Enter display name"
  - Content Type = Standard
- **EmailInput**: 
  - Placeholder = "Enter your email"
  - Content Type = Email Address
- **PasswordInput**: 
  - Placeholder = "Enter password"
  - Content Type = Password
- **ConfirmPasswordInput**: 
  - Placeholder = "Confirm password"
  - Content Type = Password
- **RegisterButton**: Text = "Create Account"
- **BackButton**: Text = "Back"
- **ErrorText**: 
  - Color = Red
  - Initially hidden (SetActive = false)

#### LoadingPanel
- **StatusText**: Text = "Loading..."
- **LoadingSpinner**: Use a simple rotating image or Unity's built-in spinner

#### ErrorPanel
- **ErrorMessageText**: 
  - Color = Red
  - Text = "An error occurred"
- **CloseButton**: Text = "Close"

### 5. Script References Setup

In the ConnectionUI component, assign all the UI references:

#### Main Panels
- Main Panel → MainPanel
- Sign In Panel → SignInPanel
- Sign Up Panel → SignUpPanel
- Loading Panel → LoadingPanel
- Error Panel → ErrorPanel

#### Main Panel Elements
- Sign In Button → SignInButton
- Sign Up Button → SignUpButton
- Guest Button → GuestButton

#### Sign In Panel Elements
- Sign In Email Input → EmailInput
- Sign In Password Input → PasswordInput
- Sign In Login Button → LoginButton
- Sign In Back Button → BackButton
- Sign In Error Text → ErrorText

#### Sign Up Panel Elements
- Sign Up Display Name Input → DisplayNameInput
- Sign Up Email Input → EmailInput
- Sign Up Password Input → PasswordInput
- Sign Up Confirm Password Input → ConfirmPasswordInput
- Sign Up Register Button → RegisterButton
- Sign Up Back Button → BackButton
- Sign Up Error Text → ErrorText

#### Loading Panel Elements
- Loading Status Text → StatusText
- Loading Spinner → LoadingSpinner

#### Error Panel Elements
- Error Message Text → ErrorMessageText
- Error Close Button → CloseButton

### 6. Build Settings

1. Open Build Settings (File → Build Settings)
2. Add the Connection scene to the build
3. Make sure it's at index 0 (first scene to load)
4. Scene order should be:
   - Connection (index 0)
   - MainMenu (index 1)
   - CharacterSelection (index 2)
   - GameplayArena (index 3)

### 7. PlayFab Settings

1. Make sure PlayFab SDK is imported
2. Configure PlayFab settings:
   - Go to Window → PlayFab → Editor Extensions
   - Set your Title ID
   - Configure any other settings as needed

### 8. Testing

1. Set Connection scene as the active scene
2. Press Play
3. Test the following flows:
   - Sign In with valid credentials
   - Sign Up with new account
   - Guest login
   - Error handling (invalid email, wrong password, etc.)
   - Successful authentication should transition to MainMenu

### 9. Styling Tips

- Use consistent colors and fonts
- Make sure buttons are large enough for mobile
- Add hover effects and animations
- Use a dark theme to match the game's aesthetic
- Add the game logo/branding to the main panel

### 10. Mobile Considerations

- Ensure touch targets are at least 44x44 pixels
- Test on different screen sizes
- Consider adding haptic feedback for button presses
- Make sure text is readable on small screens

## Troubleshooting

### Common Issues:
1. **Script references not working**: Make sure all UI elements are properly assigned in the ConnectionUI component
2. **PlayFab errors**: Verify PlayFab Title ID is correctly configured
3. **Scene transition issues**: Check that MainMenu scene is in Build Settings
4. **UI not showing**: Ensure Canvas is set up correctly and panels are initially hidden except MainPanel

### Debug Tips:
- Use Debug.Log statements in the scripts to track the flow
- Check the Console for any error messages
- Verify that PlayFabAuthManager and ConnectionManager are properly instantiated

This setup will give you a complete authentication system that integrates seamlessly with your existing game!
