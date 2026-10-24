using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Diagnostics;

namespace GimUtilities
{
	/// <summary>
	/// A static class that logs information to the internal logger (a.k.a. the console).
	/// This class can always be called and is thread safe.
	/// </summary>
	public static class GimLogger
	{
		private static Logger logger;
		private static readonly object setupLock = new object();

		/// <summary>
		/// Setup the logger from Nlog in a thread safe manner.
		/// </summary>
		private static void Setup()
		{
			lock (setupLock)
			{
				if (logger == null)
				{
					SetConfiguration();
					logger = LogManager.GetCurrentClassLogger();
				}
			}
		}

		private static void SetConfiguration()
		{
			Target logconsole = CreateColouredConsoleTarget();

			// Create configuration
			LoggingConfiguration configuration = new LoggingConfiguration();
			configuration.AddRuleForAllLevels(logconsole);

			// Set configuration
			LogManager.Configuration = configuration;
		}

		private static Target CreateColouredConsoleTarget()
		{
			// Create Console target.
			ColoredConsoleTarget logconsole = new ColoredConsoleTarget("logconsole")
			{

				// Add layout
				Layout = "${longdate}|${level:uppercase=true}: ${message}"
			};

			// Create highlighting rules.
			ConsoleRowHighlightingRule highLightingRuleInfo = ConsoleRowHighlightingRule.Default;
			highLightingRuleInfo.Condition = "level == LogLevel.Info";
			highLightingRuleInfo.ForegroundColor = ConsoleOutputColor.Gray;

			ConsoleRowHighlightingRule highLightingRuleWarn = ConsoleRowHighlightingRule.Default;
			highLightingRuleWarn.Condition = "level == LogLevel.Warn";
			highLightingRuleWarn.ForegroundColor = ConsoleOutputColor.Yellow;


			ConsoleRowHighlightingRule highLightingRuleError = ConsoleRowHighlightingRule.Default;
			highLightingRuleError.Condition = "level == LogLevel.Error";
			highLightingRuleError.ForegroundColor = ConsoleOutputColor.Red;

			// Add highlighting rules
			logconsole.RowHighlightingRules.Add(highLightingRuleInfo);
			logconsole.RowHighlightingRules.Add(highLightingRuleWarn);
			logconsole.RowHighlightingRules.Add(highLightingRuleError);

			return logconsole;
		}

		/// <summary>
		/// Write information to the logger.
		/// </summary>
		/// <param name="information"> The information to write to the logger.</param>
		public static void Info(string information)
		{
			Setup();

			lock (logger)
			{
				logger.Info(information);
			}
		}

		/// <summary>
		/// Write an integer to the logger.
		/// </summary>
		/// <param name="information"> The information to write to the logger.</param>
		public static void Info(int information)
		{
			Setup();

			lock (logger)
			{
				logger.Info(information);
			}
		}

		/// <summary>
		/// Write an object to the logger.
		/// </summary>
		/// <param name="information"> The information to write to the logger.</param>
		public static void Info(object information)
		{
			Setup();

			lock (logger)
			{
				logger.Info(information.ToString());
			}
		}

		/// <summary>
		/// Write a warning to the logger.
		/// </summary>
		/// <param name="warning"> The warning to write to the logger. </param>
		public static void Warn(string warning)
		{
			Setup();

			StackTrace stackTrace = new StackTrace();
			lock (logger)
			{
				logger.Warn(warning + " " + stackTrace.GetFrame(1).GetMethod());
			}
		}

		/// <summary>
		/// Write a error as an error to the logger.
		/// </summary>
		/// <param name="error"> The error to write to the logger. </param>
		public static Exception Error(string error)
		{
			Setup();

			StackTrace stackTrace = new StackTrace();
			lock (logger)
			{
				logger.Error(error + " " + stackTrace.ToString());
			}
            return new Exception(error);
		}

		/// <summary>
		/// Write an exception as an error to the logger. 
		/// </summary>
		/// <param name="exception"> The exception to write to the logger. </param>
		public static Exception Error(Exception exception)
		{
			Setup();

			StackTrace stackTrace = new StackTrace();
			lock (logger)
			{
				logger.Error(exception + " " + stackTrace.ToString());
			}
			return exception;
		}
	}
}
