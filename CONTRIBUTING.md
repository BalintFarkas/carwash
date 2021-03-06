# Contribute to this project

Thank you for your interest in contributing to our project!

- [Ways to contribute](#ways-to-contribute)
- [Reporting Issues](#reporting-issues)
- [Contribute using GitHub](#contribute-using-github)
- [Contribute using Git](#contribute-using-git)
- [How to use Markdown to format your topic](#how-to-use-markdown-to-format-your-topic)
- [FAQ](#faq)
- [More resources](#more-resources)

## Ways to contribute

Here are some ways you can contribute to this project:

- To report an issue, [Reporting Issues](#reporting-issues).
- To make small changes, [Contribute using GitHub](#contribute-using-github).
- To make large changes, or changes that involve code, [Contribute using Git](#contribute-using-git).
- Report bugs via GitHub Issues.

## Reporting Issues

Have you identified a reproducible problem? Have a feature request? We want to hear about it! Here's how you can make reporting your issue as effective as possible.

### Look For an Existing Issue

Before you create a new issue, please do a search in [open issues](https://github.com/mark-szabo/carwash/issues) to see if the issue or feature request has already been filed.

Be sure to scan through the [most popular](https://github.com/mark-szabo/carwash/issues?q=is%3Aopen+is%3Aissue+label%3Afeature-request+sort%3Areactions-%2B1-desc) feature requests.

If you find your issue already exists, make relevant comments and add your [reaction](https://github.com/blog/2119-add-reactions-to-pull-requests-issues-and-comments). Use a reaction in place of a "+1" comment:

- 👍 - upvote
- 👎 - downvote

If you cannot find an existing issue that describes your bug or feature, create a new issue using the guidelines below.

### Writing Good Bug Reports and Feature Requests

File a single issue per problem and feature request. Do not enumerate multiple bugs or feature requests in the same issue.

Do not add your issue as a comment to an existing issue unless it's for the identical input. Many issues look similar, but have different causes.

The more information you can provide, the more likely someone will be successful at reproducing the issue and finding a fix.

Please include the following with each issue:

- Version

- Your operating system

- Reproducible steps (1... 2... 3...) that cause the issue

- What you expected to see, versus what you actually saw

- Images, animations, or a link to a video showing the issue occurring

- A code snippet that demonstrates the issue or a link to a code repository the developers can easily pull down to recreate the issue locally

  **Note:** Because the developers need to copy and paste the code snippet, including a code snippet as a media file (i.e. .gif) is not sufficient.

- Errors from the Dev Tools Console (open from the menu: More tools > Developer tools)

### Final Checklist

Please remember to do the following:

- [ ] Search the issue repository to ensure your report is a new issue

- [ ] Recreate the issue after disabling all extensions

- [ ] Simplify your code around the issue to better isolate the problem

Don't feel bad if the developers can't reproduce the issue right away. They will simply ask for more information!

## Contribute using GitHub

Use GitHub to contribute to this project without having to clone the repo to your desktop. This is the easiest way to create a pull request in this repository. Use this method to make a minor change that doesn't involve code changes.

### To Contribute using GitHub

1. Find the file you want to edit.
2. Once you are there, sign in to GitHub (get a free account [Join GitHub](https://github.com/join).
3. Choose the **pencil icon** (edit the file in your fork of this project) and make your changes in the **<>Edit file** window.
4. Scroll to the bottom and enter a description.
5. Choose **Propose file change**>**Create pull request**.

You now have successfully submitted a pull request. Pull requests are typically reviewed within 10 business days.

## Contribute using Git

Use Git to contribute substantive changes, such as:

- Contributing code.
- Contributing changes that affect meaning.
- Contributing large changes to text.

### To Contribute using Git

1. If you don't have a GitHub account, set one up at [GitHub](https://github.com/join).
2. After you have an account, install Git on your computer. Follow the steps in [Setting up Git Tutorial](https://help.github.com/articles/set-up-git/).
3. To submit a pull request using Git, follow the steps in [Use GitHub, Git, and this repository](#use-github-git-and-this-repository).
4. You will be asked to sign the Contributor's License Agreement if you are:

   - A member of the Microsoft Open Technologies group.
   - A contributors who doesn't work for Microsoft.

As a community member, you must sign the Contribution License Agreement (CLA) before you can contribute large submissions to a project. You only need to complete and submit the agreement once. Carefully review the document. You may be required to have your employer sign the document.

Signing the CLA does not grant you rights to commit to the main repository, but it does mean that our team will be able to review and approve your contributions. You will be credited for your submissions.

Pull requests are typically reviewed within 10 business days.

## Use GitHub, Git, and this repository

**Note:** Most of the information in this section can be found in [GitHub Help] articles. If you're familiar with Git and GitHub, skip to the **Contribute and edit content** section for the specifics of the code/content flow of this repository.

### To set up your fork of the repository

1.  Set up a GitHub account so you can contribute to this project. If you haven't done this, go to [GitHub](https://github.com/join) and do it now.
2.  Install Git on your computer. Follow the steps in the [Setting up Git Tutorial][set up git].
3.  Create your own fork of this repository. To do this, at the top of the page, choose the **Fork** button.
4.  Copy your fork to your computer. To do this, open Git Bash. At the command prompt enter:

        git clone https://github.com/<your user name>/<repo name>.git

    Next, create a reference to the root repository by entering these commands:

        cd <repo name>
        git remote add upstream https://github.com/mark-szabo/<repo name>.git
        git fetch upstream

Congratulations! You've now set up your repository. You won't need to repeat these steps again.

### Contribute and edit content

To make the contribution process as seamless as possible, follow these steps.

#### To contribute and edit content

1. Create a new branch.
2. Add new content or edit existing content.
3. Submit a pull request to the main repository.
4. Delete the branch.

**Important** Limit each branch to a single concept to streamline the work flow and reduce the chance of merge conflicts.

#### To create a new branch

1. Open Git Bash.
2. At the Git Bash command prompt, type `git pull upstream master:<new branch name>`. This creates a new branch locally that is copied from the latest master branch.
3. At the Git Bash command prompt, type `git push origin <new branch name>`. This alerts GitHub to the new branch. You should now see the new branch in your fork of the repository on GitHub.
4. At the Git Bash command prompt, type `git checkout <new branch name>` to switch to your new branch.

#### Add new content or edit existing content

You navigate to the repository on your computer by using File Explorer. The repository files are in `C:\Users\<yourusername>\<repo name>`.

To edit files, open them in an editor of your choice and modify them. To create a new file, use the editor of your choice and save the new file in the appropriate location in your local copy of the repository. While working, save your work frequently.

The files in `C:\Users\<yourusername>\<repo name>` are a working copy of the new branch that you created in your local repository. Changing anything in this folder doesn't affect the local repository until you commit a change. To commit a change to the local repository, type the following commands in GitBash:

    git add .
    git commit -v -a -m "<Describe the changes made in this commit>"

The `add` command adds your changes to a staging area in preparation for committing them to the repository. The period after the `add` command specifies that you want to stage all of the files that you added or modified, checking subfolders recursively. (If you don't want to commit all of the changes, you can add specific files. You can also undo a commit. For help, type `git add -help` or `git status`.)

The `commit` command applies the staged changes to the repository. The switch `-m` means you are providing the commit comment in the command line. The -v and -a switches can be omitted. The -v switch is for verbose output from the command, and -a does what you already did with the add command.

You can commit multiple times while you are doing your work, or you can commit once when you're done.

#### Submit a pull request to the main repository

When you're finished with your work and are ready to have it merged into the main repository, follow these steps.

#### To submit a pull request to the main repository

1. In the Git Bash command prompt, type `git push origin <new branch name>`. In your local repository, `origin` refers to your GitHub repository that you cloned the local repository from. This command pushes the current state of your new branch, including all commits made in the previous steps, to your GitHub fork.
2. On the GitHub site, navigate in your fork to the new branch.
3. Choose the **Pull Request** button at the top of the page.
4. Verify the Base branch is `mark-szabo/<repo name>@master` and the Head branch is `<your username>/<repo name>@<branch name>`.
5. Choose the **Update Commit Range** button.
6. Add a title to your pull request, and describe all the changes you're making.
7. Submit the pull request.

One of the site administrators will process your pull request. Your pull request will surface on the `mark-szabo/<repo name>` site under Issues. When the pull request is accepted, the issue will be resolved.

#### Create a new branch after merge

After a branch is successfully merged (that is, your pull request is accepted), don't continue working in that local branch. This can lead to merge conflicts if you submit another pull request. To do another update, create a new local branch from the successfully merged upstream branch, and then delete your initial local branch.

For example, if your local branch X was successfully merged into the master branch and you want to make additional updates to the content that was merged. Create a new local branch, X2, from the master branch. To do this, open GitBash and execute the following commands:

    cd <repo name>
    git pull upstream master:X2
    git push origin X2

You now have local copies (in a new local branch) of the work that you submitted in branch X. The X2 branch also contains all the work other writers have merged, so if your work depends on others' work (for example, shared images), it is available in the new branch. You can verify that your previous work (and others' work) is in the branch by checking out the new branch...

    git checkout X2

...and verifying the content. (The `checkout` command updates the files in `C:\Users\<yourusername>\<repo name>` to the current state of the X2 branch.) Once you check out the new branch, you can make updates to the content and commit them as usual. However, to avoid working in the merged branch (X) by mistake, it's best to delete it (see the following **Delete a branch** section).

#### Delete a branch

Once your changes are successfully merged into the main repository, delete the branch you used because you no longer need it. Any additional work should be done in a new branch.

#### To delete a branch

1. In the Git Bash command prompt, type `git checkout master`. This ensures that you aren't in the branch to be deleted (which isn't allowed).
2. Next, at the command prompt, type `git branch -d <branch name>`. This deletes the branch on your computer only if it has been successfully merged to the upstream repository. (You can override this behavior with the `–D` flag, but first be sure you want to do this.)
3. Finally, type `git push origin :<branch name>` at the command prompt (a space before the colon and no space after it). This will delete the branch on your github fork.

Congratulations, you have successfully contributed to the project!

## How to use Markdown to format your topic

A complete introduction (and listing of all the syntax) can be found at [Markdown Home].

## FAQ

### How do I get a GitHub account?

Fill out the form at [Join GitHub](https://github.com/join) to open a free GitHub account.

### Where do I get a Contributor's License Agreement?

You will automatically be sent a notice that you need to sign the Contributor's License Agreement (CLA) if your pull request requires one.

As a community member, **you must sign the Contribution License Agreement (CLA) before you can contribute large submissions to this project**. You only need complete and submit the agreement once. Carefully review the document. You may be required to have your employer sign the document.

### What happens with my contributions?

When you submit your changes, via a pull request, our team will be notified and will review your pull request. You will receive notifications about your pull request from GitHub; you may also be notified by someone from our team if we need more information. We reserve the right to edit your submission for legal, style, clarity, or other issues.

### Can I become an approver for this repository's GitHub pull requests?

Currently, we are not allowing external contributors to approve pull requests in this repository.

### How soon will I get a response about my change request or issue?

We typically review pull requests and respond to issues within 10 business days.

## More resources

- To learn more about Markdown, go to the Git creator's site [Daring Fireball].
- To learn more about using Git and GitHub, first check out the [GitHub Help section][github help].

[github home]: http://github.com
[github help]: http://help.github.com/
[set up git]: http://help.github.com/win-set-up-git/
[markdown home]: http://daringfireball.net/projects/markdown/
[daring fireball]: http://daringfireball.net/
