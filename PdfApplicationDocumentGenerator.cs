using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext DataContext;
		private IPathProvider _templatePathProvider;
		public IViewGenerator View_Generator;
		internal readonly IConfiguration _configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
		private readonly IPdfGenerator _pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			if (dataContext != null)
				throw new ArgumentNullException(nameof(dataContext));
			
			DataContext = dataContext;
			_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException("templatePathProvider");
			View_Generator = viewGenerator;
			_configuration = configuration;
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_pdfGenerator = pdfGenerator;
		}
		
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			Application application = DataContext.Applications.Single(app => app.Id == applicationId);

			if (application != null)
			{
				return GeneratePdfApplication(ref baseUri, application);
			}
			else
			{
				
				_logger.LogWarning($"No application found for id '{applicationId}'");
				return null;
			}
		}

		private byte[] GeneratePdfApplication(ref string baseUri, Application application)
		{
			if (baseUri.EndsWith("/"))
				baseUri = baseUri.Substring(baseUri.Length - 1);

			string view;

			if (application.State != ApplicationState.Closed)
			{
				view = ApplicationViewByState(baseUri, application);
			}
			else
			{
				_logger.LogWarning(
					$"The application is in state '{application.State}' and no valid document can be generated for it.");
				return null;
			}

			var pdfOptions = new PdfOptions
			{
				PageNumbers = PageNumbers.Numeric,
				HeaderOptions = new HeaderOptions
				{
					HeaderRepeat = HeaderRepeat.FirstPageOnly,
					HeaderHtml = PdfConstants.Header
				}
			};
			var pdf = _pdfGenerator.GenerateFromHtml(view, pdfOptions);
			return pdf.ToBytes();
		}

		private string ApplicationViewByState(string baseUri, Application application) 
		{
			string view;
			string path = null;
			ApplicationViewModel vm = null;
			if (application.State == ApplicationState.Activated)
			{
				path = _templatePathProvider.Get("ActivatedApplication");
			}
			else if (application.State == ApplicationState.Pending)
			{
				path = _templatePathProvider.Get("PendingApplication");
			}
			else if (application.State == ApplicationState.InReview) 
			{
				path = _templatePathProvider.Get("InReviewApplication");
			}

			if (application.State == ApplicationState.InReview)
			{
				vm = GetApplicationViewModelInReview(application);
			}
			else
			{
				vm = GetApplicationViewModel(application);
			}
			view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), vm);
			return view;
		}

		private ApplicationViewModel GetApplicationViewModel(Application application)
		{
			ApplicationViewModel vm;
			ApplicationViewModel applicationViewModel = new ApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = string.Format(
					"{0} {1}",
					application.Person.FirstName,
					application.Person.Surname),
				LegalEntity =
					application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products.SelectMany(p => p.Funds),
				PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
				.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
				.Sum(),
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature
			};
			vm = applicationViewModel;
			return vm;
		}

		private ApplicationViewModel GetApplicationViewModelInReview(Application application)
		{
			ApplicationViewModel vm;
			var inReviewMessage = "Your application has been placed in review" +
			application.CurrentReview.Reason switch
			{
			{ } reason when reason.Contains("address") =>
				 " pending outstanding address verification for FICA purposes.",
			{ } reason when reason.Contains("bank") =>
				" pending outstanding bank account verification.",
			 _ =>
				 " because of suspicious account behaviour. Please contact support ASAP."
			};

			InReviewApplicationViewModel inReviewApplicationViewModel = new InReviewApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = string.Format(
				"{0} {1}",
				application.Person.FirstName,
				application.Person.Surname),
				LegalEntity =
				application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products.SelectMany(p => p.Funds),
				PortfolioTotalAmount = application.Products.SelectMany(p => p.Funds)
				.Select(f => (f.Amount - f.Fees) * _configuration.TaxRate)
				.Sum(),
				AppliedOn = application.Date,
				SupportEmail = _configuration.SupportEmail,
				Signature = _configuration.Signature,
				InReviewMessage = inReviewMessage,
				InReviewInformation = application.CurrentReview
			};
			vm = inReviewApplicationViewModel;
			return vm;
		}
	}
}
