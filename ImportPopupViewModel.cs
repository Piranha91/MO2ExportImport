using ReactiveUI;
using System.Reactive;

namespace MO2ExportImport.ViewModels
{
    public class ImportPopupViewModel : ReactiveObject
    {
        private readonly string _mo2Directory;
        private readonly string _selectedProfile;
        private readonly string _exportDirectory;
        private readonly ImportPopupView _window;

        public ImportPopupViewModel(ImportPopupView window, string mo2Directory, string selectedProfile, string exportDirectory)
        {
            _window = window;
            _mo2Directory = mo2Directory;
            _selectedProfile = selectedProfile;
            _exportDirectory = exportDirectory;

            ImportCommand = ReactiveCommand.Create(Import);
            CancelCommand = ReactiveCommand.Create(Cancel);
        }

        public ReactiveCommand<Unit, Unit> ImportCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        private void Import()
        {
            // Logic for importing from the export folder to the selected MO2 directory/profile(s)
        }

        private void Cancel()
        {
            // Close the popup
        }
    }
}
