using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BedrockWireAuthDump.Model
{
	public class ApiResponse<T>
	{
		public T Result { get; set; }
		public HttpStatusCode StatusCode { get; set; }
		public bool IsSuccess { get; set; } = false;

		public ApiResponse(bool isSuccess, HttpStatusCode statusCode, T result)
		{
			IsSuccess = isSuccess;
			StatusCode = statusCode;
			Result = result;
		}
	}
}
