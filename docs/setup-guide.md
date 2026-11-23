# Setup guide for Pytt

This guide gets your PC ready so Claude can build the game there and you can play each new version. It takes about **an hour**, mostly waiting for downloads. You only do it once.

If anything looks different from these steps, take a screenshot and send it in the project. That's completely fine.

---

## Before you start: check your PC

You need:

- **Windows 10 or 11 (64-bit)**, or a **Mac** with macOS 12 or newer.
- **16 GB of memory (RAM)** is recommended. 8 GB works, but slowly.
- **At least 30 GB of free disk space.**
- A reasonably recent graphics card. Most gaming PCs and laptops from the last 6 years are fine.

*How to check on Windows:* press the **Windows key**, type **About your PC**, and open it. It shows your memory ("Installed RAM") and Windows version. To check disk space, open **File Explorer** and click **This PC**.

*How to check on a Mac:* click the **Apple menu** in the top-left, then **About This Mac**.

---

## Step 1: Create a free Unity account

1. Go to **https://unity.com** and click **Sign in**, then **Create a Unity ID**.
2. Sign up with your email, or with Google or Apple.
3. Confirm your email when Unity sends you a message.

## Step 2: Install Unity Hub

Unity Hub is a small app that installs and opens Unity, a bit like a launcher such as Steam.

1. Go to **https://unity.com/download**.
2. Click the download button for your system (Windows or Mac).
3. Open the downloaded file and follow the installer, clicking **Next** / **Agree** / **Install** with the normal options.
4. Open **Unity Hub** and sign in with the Unity account from Step 1.
5. If it asks about a licence, choose **Unity Personal** (free). It is free until a game earns a large amount of money each year, so it's fine for us.

## Step 3: Install the Unity editor

1. In Unity Hub, click **Installs** on the left.
2. Click **Install Editor**.
3. Pick the newest version labelled **LTS** that starts with **6** (for example "Unity 6.x LTS"). LTS means "long-term support": the stable version.
4. On the next screen, tick:
   - **Windows Build Support** (on Windows it may already be included), so we can make a playable game file.
   - **Visual Studio** if it is offered (a free code editor Unity uses behind the scenes).
   - Leave everything else as it is.
5. Click **Install** and wait. This is the long download (several GB, often 20 to 40 minutes).

## Step 4: Install GitHub Desktop

GitHub Desktop is a simple app that keeps the game files on your PC in sync with the GitHub repository, where Claude saves its work.

1. Go to **https://desktop.github.com** and download it.
2. Install it and sign in with the same GitHub account you added to this project (**pytt286-dotcom**).
3. Click **File > Clone repository**, choose **G001** from the list, and click **Clone**. Note the folder it saves to (usually `Documents\GitHub\G001`).

## Step 5: Tell Claude you're done

Reply in the project with:

- "Setup done", and
- whether you're on **Windows or Mac**.

Claude then takes it from there: it creates the Sealbound project inside your G001 folder, sets it up, and saves it to GitHub. When Claude needs your PC, the Claude app may ask you to **approve access to the G001 folder**. Please say yes to that.

---

## Later: how to try a new version

Once the project exists, trying a new version will look like this:

1. In **GitHub Desktop**, click **Fetch origin**, then **Pull origin** if it appears. This downloads Claude's latest work.
2. In **Unity Hub**, click **Projects** and open **Sealbound**.
3. In Unity, press the **Play** button (the triangle at the top centre).
4. Play, then press Play again to stop. Tell Claude what you think.

Claude will remind you of these steps each time until they feel familiar.
