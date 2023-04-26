using CredentialManagement;
using LibGit2Sharp.Handlers;
using Octokit;
using Octokit.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GitHubClient
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<string> _repos = new ObservableCollection<string>();

        public ObservableCollection<string> Repos
        {
            get { return _repos; }
            set { _repos = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public ICommand UploadCommand { get; set; }

        public ICommand GetCommand { get; set; }

        public MainWindowViewModel()
        {
            UploadCommand = new DelegateCommand(OnUpload);
            GetCommand = new DelegateCommand(OnGet);
        }

        public async void OnUpload()
        {
            const string gitHubUserName = "kevmoens";
            const string gitHubOrganization = "kevmoens";

            var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("repo"));
            string password = string.Empty;
            using (var cred = new Credential())
            {
                cred.Target = "https://github.com";
                cred.Load();
                password = cred.Password;
                var tokenAuth = new Credentials(cred.Password);
                client.Credentials = tokenAuth;
            }

            var repoPath = LibGit2Sharp.Repository.Discover(System.Environment.CurrentDirectory);
            using (var localRepo = new LibGit2Sharp.Repository(repoPath))
            {

                DirectoryInfo dir = new DirectoryInfo(localRepo.Info.WorkingDirectory);
                string remoteUrl = $"https://github.com/{gitHubOrganization}/{dir.Name}.git";
                LibGit2Sharp.Remote remote = null;

                if (localRepo.Network.Remotes.Any(r => r.Url == remoteUrl))
                {
                    remote = localRepo.Network.Remotes.FirstOrDefault(r => r.Url == remoteUrl);
                }
                else
                {
                    remote = localRepo.Network.Remotes.Add("github", remoteUrl);
                }

                Repository githubRepo = null;
                //Create a new repo in GitHub
                try
                {
                    githubRepo = await client.Repository.Get(gitHubOrganization, dir.Name);
                }
                catch
                {
                    var newRepo = new NewRepository(dir.Name);
                    newRepo.Private = true;
                    githubRepo = await client.Repository.Create(newRepo);
                }


                // Add Refs for Remotes 
                foreach (var branch in localRepo.Branches)
                {
                    if (!branch.IsRemote)
                    {
                        // Create a new tracking branch
                        var trackingRef = "refs/heads/" + branch.FriendlyName;
                        var remoteRef = "refs/remotes/" + remote.Name + "/" + branch.FriendlyName;
                        if (localRepo.Refs.Any(r => r.CanonicalName == remoteRef))
                        {
                            localRepo.Refs.Remove(remoteRef);
                        } 
                        localRepo.Refs.Add(remoteRef, new LibGit2Sharp.ObjectId(branch.Tip.Sha));
                        localRepo.Refs.UpdateTarget(trackingRef, branch.Tip.Sha, "Created by LibGit2Sharp");                        
                    }
                }

                // Pushed tracked Remote Branches
                foreach (var branch in localRepo.Branches)
                {
                    if (branch.IsRemote)
                    {

                        // Push the branch to the remote                        
                        var pushOptions = new LibGit2Sharp.PushOptions();
                        pushOptions.CredentialsProvider = new LibGit2Sharp.Handlers.CredentialsHandler(
                           (url, usernameFromUrl, types) =>
                               new LibGit2Sharp.UsernamePasswordCredentials()
                               {                                   
                                   Username = gitHubUserName,
                                   Password = password
                               });
                        localRepo.Network.Push(branch, pushOptions);
                    }
                }

            }
            MessageBox.Show("Done");

        }

        public async void OnGet()
        {
            var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("repo"));

            using (var cred = new Credential())
            {
                cred.Target = "https://github.com";
                cred.Load();
                var tokenAuth = new Credentials(cred.Password);
                client.Credentials = tokenAuth;
            }

            foreach (var repo in await client.Repository.GetAllForCurrent())
            {
                Repos.Add(repo.Name + " " + repo.CloneUrl);
            }
            MessageBox.Show(Repos.Count.ToString());
        }
    }
}
